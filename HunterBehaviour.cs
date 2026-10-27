using UnityEngine;

namespace Hufsa
{
    /// <summary>
    /// Replaces MonsterAI (whose UpdateAI is skipped in Patches). Runs on the Hunter's owner.
    /// - Hunts the nearest huntable player (not indoors, in bed, in a dungeon, on a boat, swimming, at sea
    ///   or in the Ashlands), switching to another one nearby when its target finds safety.
    /// - Speed depends on distance: a slow glide when close, growing exponentially as you pull away, up to
    ///   just under a fresh player's run, so with the cold sapping stamina you can only barely escape.
    /// - Nature doesn't stop it (rocks, trees, logs); walls and ruins do, so it pathfinds around them.
    ///   It never goes into deep water.
    /// - It leaves when you escape (EscapeDistance), at dawn, or when everyone near it has been safe for
    ///   SafeSeconds; in that last case the server sends it after another player if there is one.
    /// </summary>
    internal class HunterBehaviour : MonoBehaviour
    {
        private const float StuckCheckSeconds = 2f;
        private const float PathfindSeconds = 6f;

        private HufsaEntity entity;
        private Character character;
        private BaseAI ai;
        private Collider body;
        private Vector3 lastProgressPos;
        private float stuckTimer;
        private float pathfindUntil;
        private float scanTimer;
        private float safeTimer;
        private float waitCloseTimer;
        private float noProgress;

        // The lunge: a Hunter that grew out of the Watcher you kept seeing rushes at you first.
        public const string LungeKey = "hufsa_lunge";
        private float lungeStart = -1f;
        private bool lungeChecked;
        private readonly Collider[] nearby = new Collider[96];
        private int scanMask;

        private void Awake()
        {
            entity = GetComponent<HufsaEntity>();
            character = GetComponent<Character>();
            ai = GetComponent<BaseAI>();
            body = GetComponent<CapsuleCollider>();
            scanMask = LayerMask.GetMask("Default", "static_solid", "Default_small", "piece", "piece_nonsolid");
            character.m_acceleration = 2f;
            SetSpeed(HufsaConfig.HunterSpeed.Value);
            lastProgressPos = transform.position;
        }

        private void FixedUpdate()
        {
            ZNetView nview = entity.NView;
            if (!nview || !nview.IsValid() || !nview.IsOwner()) return;

            float dt = Time.fixedDeltaTime;
            if (entity.Vanishing)
            {
                ai.StopMoving();
                return;
            }

            Vector3 pos = transform.position;
            if (!entity.Forced && !HufsaEntity.IsHufsaTime(pos))
            {
                Leave(); // dawn
                return;
            }

            // There is no outrunning it: distance never makes it give up (if it loses you, the server sends it
            // after you again). Only shelter does.
            Player prey = NearestPlayer(pos, true, out float preyDist);
            Player anyone = NearestPlayer(pos, false, out float anyDist);

            if (!prey)
            {
                if (!anyone)
                {
                    Leave(); // nobody left here at all
                    return;
                }
                // Everyone is in shelter (or on a boat...). It comes and waits outside. It gives up after a minute,
                // or after 10 s if it is pressed right up against the walls (within 7 m) and can't get closer.
                safeTimer += dt;
                waitCloseTimer = anyDist <= HufsaConfig.SafeCloseDistance.Value ? waitCloseTimer + dt : 0f;
                if (safeTimer >= HufsaConfig.SafeSeconds.Value || waitCloseTimer >= HufsaConfig.SafeCloseSeconds.Value)
                {
                    HufsaSpawner.ReportPreySafe(anyone.GetOwner());
                    Leave();
                    return;
                }
                if (door) EndDoor(false); // you made it to safety: it stops trying to get in
                EndClimb();
                Chase(anyone, anyDist, dt, allowDoors: false);
                return;
            }
            waitCloseTimer = 0f;

            // Going in after you through a door.
            if (door)
            {
                safeTimer = 0f;
                UseDoor(prey, dt);
                return;
            }

            // Up on a rock, a cliff or a steep slope: it climbs after you, slowly. Stuck on a hill or a rock between
            // you: it goes up and over it (traversing).
            if (climbing || traversing || WantsToClimb(prey, pos))
            {
                safeTimer = 0f;
                Climb(prey, dt);
                return;
            }

            // Made no progress for a long time on level ground (walled in somehow): that counts as safe.
            if (noProgress > 12f)
            {
                safeTimer += dt;
                if (safeTimer >= HufsaConfig.SafeSeconds.Value)
                {
                    HufsaSpawner.ReportPreySafe(prey.GetOwner());
                    Leave();
                    return;
                }
            }
            else
            {
                safeTimer = 0f;
            }
            Chase(prey, preyDist, dt);
        }

        private void Leave()
        {
            if (door) EndDoor(false);
            EndClimb();
            EndGlide();
            ai.StopMoving();
            entity.Vanish();
        }

        // ---------------------------------------------------------------- climbing

        /// <summary>True while climbing or gliding over water; Patches then skips its normal physics (gravity, ground contact).</summary>
        public bool Climbing => climbing || gliding;

        private bool climbing;
        private float climbTime;
        private readonly RaycastHit[] surfaceHits = new RaycastHit[16];
        private int surfaceMask;

        private bool WantsToClimb(Player prey, Vector3 pos)
        {
            Vector3 d = prey.transform.position - pos;
            float up = d.y;
            d.y = 0f;
            return (up > 2.5f && d.magnitude < 12f) || (noProgress > 4f && up > 1.2f);
        }

        /// <summary>
        /// Physics off; it creeps toward its prey over the top of whatever is in the way. Facing a rise (a rock
        /// face, a cliff, a steep slope) it goes straight up at ClimbSpeed, then moves on across the top. It stops
        /// climbing once its prey is no longer above it and it is standing on something again.
        /// </summary>
        private void Climb(Player prey, float dt)
        {
            if (!climbing)
            {
                EndGlide();
                climbing = true;
                climbTime = 0f;
                ai.StopMoving();
                character.SetMoveDir(Vector3.zero);
                if (character.m_body) character.m_body.isKinematic = true;
                if (surfaceMask == 0)
                    surfaceMask = LayerMask.GetMask("terrain", "static_solid", "Default", "Default_small", "piece");
            }
            climbTime += dt;

            Vector3 pos = transform.position;
            Vector3 preyPos = prey.transform.position;
            Vector3 to = preyPos - pos;
            float preyAbove = to.y;
            to.y = 0f;
            Vector3 dir = to.sqrMagnitude > 0.01f ? to.normalized : transform.forward;
            if (to.sqrMagnitude > 0.01f) character.SetLookDir(dir, 0f);

            // Climbing up to you is slow on purpose; getting over something in the way goes at the chase speed.
            float speed = HufsaConfig.ClimbSpeed.Value;
            float across = speed * 1.5f;
            if (traversing)
            {
                speed = Mathf.Max(speed, chaseSpeed * 0.7f);
                across = Mathf.Max(across, chaseSpeed);
            }
            float top = Mathf.Max(pos.y, preyPos.y) + 8f;
            float under = SurfaceAt(pos, top);
            float ahead = SurfaceAt(pos + dir * 1.2f, top);

            Vector3 next = pos;
            if (to.magnitude > 1.4f && ahead > pos.y + 0.4f)
            {
                next.y += speed * dt; // a face in front: up
            }
            else if (to.magnitude > 1.4f)
            {
                next += dir * across * dt; // across the top
                float s = SurfaceAt(next, top);
                next.y = s > next.y ? Mathf.MoveTowards(next.y, s, speed * 2f * dt) : Mathf.MoveTowards(next.y, s, 3f * dt);
            }
            else
            {
                next.y = Mathf.MoveTowards(next.y, under, 3f * dt); // there: settle onto the surface
            }
            if (character.m_body) character.m_body.position = next;
            transform.position = next;

            bool onSurface = Mathf.Abs(next.y - SurfaceAt(next, top)) < 0.3f;
            if (traversing)
            {
                // over it once it has gone a few metres on and nothing rises in front any more
                Vector3 gone = next - traverseStart;
                gone.y = 0f;
                if ((onSurface && gone.magnitude > 3f && ahead <= next.y + 0.4f) || climbTime > 12f) traversing = false;
            }
            if (!traversing && preyAbove < 1f && onSurface) EndClimb();
            else if (climbTime > 60f)
            {
                // Climbed for a minute without getting there: give up like with any safe spot.
                HufsaSpawner.ReportPreySafe(prey.GetOwner());
                Leave();
            }
        }

        private bool traversing;
        private Vector3 traverseStart;
        private float chaseSpeed;

        /// <summary>Blocked by a hill or a rock (not a building): go up and over it like climbing, at chase speed.</summary>
        private void StartTraverse()
        {
            traversing = true;
            traverseStart = transform.position;
            HufsaPlugin.Log.LogInfo("Hufsa is going over what's in its way");
        }

        /// <summary>A player build right in front (walls, fences): those it goes around or through the door, never over.</summary>
        private bool BuildingAhead(Vector3 pos, Vector3 dir)
        {
            if (pieceMask == 0) pieceMask = LayerMask.GetMask("piece");
            int n = Physics.OverlapSphereNonAlloc(pos + dir * 2f + Vector3.up * 1.2f, 1.3f, doorHits, pieceMask,
                QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++)
            {
                Collider c = doorHits[i];
                if (c && c != body && c.GetComponentInParent<Piece>()) return true;
            }
            return false;
        }

        private int pieceMask;

        private void EndClimb()
        {
            traversing = false;
            if (!climbing) return;
            climbing = false;
            noProgress = 0f;
            lastProgressPos = transform.position;
            if (character.m_body)
            {
                character.m_body.isKinematic = false;
                character.m_body.linearVelocity = Vector3.zero;
            }
        }

        /// <summary>Highest solid surface under this point (terrain, rock, building), ignoring trees and itself.</summary>
        private float SurfaceAt(Vector3 p, float fromHeight)
        {
            var origin = new Vector3(p.x, fromHeight, p.z);
            int n = Physics.RaycastNonAlloc(origin, Vector3.down, surfaceHits, fromHeight - p.y + 30f, surfaceMask,
                QueryTriggerInteraction.Ignore);
            float best = float.MinValue;
            for (int i = 0; i < n; i++)
            {
                Collider c = surfaceHits[i].collider;
                if (!c || c == body || c.GetComponentInParent<TreeBase>() || c.GetComponentInParent<Character>()) continue;
                best = Mathf.Max(best, surfaceHits[i].point.y);
            }
            if (best > float.MinValue) return best;
            return ZoneSystem.instance.GetGroundHeight(p, out float h) ? h : p.y;
        }

        private static Player NearestPlayer(Vector3 pos, bool huntableOnly, out float distance)
        {
            Player best = null;
            distance = float.MaxValue;
            foreach (Player p in Player.GetAllPlayers())
            {
                if (!p || p.IsDead()) continue;
                if (huntableOnly && !HufsaHuntState.IsHuntable(p)) continue;
                float d = Vector3.Distance(p.transform.position, pos);
                if (d < distance)
                {
                    distance = d;
                    best = p;
                }
            }
            return best;
        }

        /// <summary>
        /// The opening rush: a burst at RushSpeed (faster than you can run) for RushSeconds, then a smooth ease-out
        /// into the normal chase over RushEaseOutSeconds (also as soon as it is within RushStopDistance, so the
        /// burst can't carry it all the way to you). Only for Hunters born from your last Watcher sighting.
        /// Returns the speed to use, blending from the rush to the normal chase speed.
        /// </summary>
        private float RushBlend(float dist, float normal, float dt)
        {
            if (!lungeChecked)
            {
                lungeChecked = true;
                ZNetView nview = entity.NView;
                if (nview && nview.IsValid() && nview.GetZDO().GetBool(LungeKey, false))
                {
                    lungeStart = Time.time;
                    rush = 1f;
                    nview.GetZDO().Set(LungeKey, false); // once, even if another client takes over
                }
            }
            if (rush <= 0f) return normal;

            bool bursting = Time.time - lungeStart < HufsaConfig.LungeSeconds.Value && dist >= HufsaConfig.LungeStopDistance.Value;
            if (!bursting) rush = Mathf.MoveTowards(rush, 0f, dt / Mathf.Max(0.1f, HufsaConfig.RushEaseOut.Value));
            float e = rush * rush * (3f - 2f * rush); // smoothstep: no sudden braking at either end
            return Mathf.Lerp(normal, Mathf.Max(normal, HufsaConfig.LungeSpeed.Value), e);
        }

        private float rush;

        /// <param name="allowDoors">False while its target is safe in shelter: it waits outside and never opens the door.</param>
        private void Chase(Player target, float dist, float dt, bool allowDoors = true)
        {
            Vector3 pos = transform.position;
            Vector3 toTarget = target.transform.position - pos;
            toTarget.y = 0f;
            if (toTarget.sqrMagnitude > 0.01f) character.SetLookDir(toTarget.normalized, 0f);

            // Slow when close, exponentially faster the further you get, capped just under a running player.
            float close = HufsaConfig.HunterCloseDistance.Value;
            float speed = HufsaConfig.HunterSpeed.Value;
            if (dist > close) speed *= Mathf.Exp((dist - close) / Mathf.Max(HufsaConfig.HunterSpeedScale.Value, 1f));
            speed = Mathf.Min(speed, HufsaConfig.HunterMaxSpeed.Value);
            chaseSpeed = RushBlend(dist, speed, dt);
            SetSpeed(chaseSpeed);

            if (toTarget.magnitude < 1.6f || OceanAhead(pos, toTarget.normalized))
            {
                if (gliding) GlideOverWater(Vector3.zero, dt); // stay put on the water, don't sink
                else
                {
                    ai.StopMoving();
                    character.SetMoveDir(Vector3.zero);
                }
                stuckTimer = 0f;
                lastProgressPos = pos;
                return;
            }

            // Rivers and lakes (not the ocean): it glides straight across on the surface.
            if (Wet(pos) || Wet(pos + toTarget.normalized * 2.5f))
            {
                GlideOverWater(toTarget.normalized, dt);
                stuckTimer = 0f;
                lastProgressPos = pos;
                return;
            }
            if (gliding) EndGlide();

            scanTimer -= dt;
            if (scanTimer <= 0f)
            {
                IgnoreNatureAround(pos);
                scanTimer = 0.5f;
            }

            if (Time.time < pathfindUntil) ai.MoveTo(dt, target.transform.position, 1.6f, false);
            else character.SetMoveDir(toTarget.normalized);

            // Getting no closer? Progress is measured toward the target, so shaking in place against a slope or a
            // rock counts as stuck too (while pathfinding around something, any real movement counts).
            // A building in the way: it goes for the door if you're inside, otherwise finds a way around.
            // Anything else (a hill, a rock, a ruin): it goes up and over it.
            stuckTimer += dt;
            if (stuckTimer > StuckCheckSeconds)
            {
                Vector3 moved = pos - lastProgressPos;
                moved.y = 0f;
                bool stuck = Time.time < pathfindUntil
                    ? moved.sqrMagnitude < 0.25f
                    : Vector3.Dot(moved, toTarget.normalized) < 0.6f;
                if (stuck && dist > 3f)
                {
                    if (allowDoors && !BuildingAhead(pos, toTarget.normalized)) StartTraverse();
                    else if (!(allowDoors && TryGoForDoor(target))) pathfindUntil = Time.time + PathfindSeconds;
                    noProgress += stuckTimer;
                }
                else
                {
                    noProgress = 0f;
                }
                lastProgressPos = pos;
                stuckTimer = 0f;
            }
        }

        // ---------------------------------------------------------------- doors

        private enum DoorStep { Approach, Open, Through }

        private Door door;
        private DoorStep doorStep;
        private float doorStepTime;
        private bool doorPushed;
        private Vector3 doorOutside, doorInside;
        private float normalRadius = -1f;
        private Door lastFailedDoor;
        private readonly Collider[] doorHits = new Collider[128];

        /// <summary>
        /// Stuck outside a building with its prey inside: pick the door that makes the shortest way in (from here,
        /// to the door, to the prey), within 25 m of the prey. Locked doors (keys, dungeons) are left alone.
        /// </summary>
        private bool TryGoForDoor(Player prey)
        {
            int mask = LayerMask.GetMask("piece", "piece_nonsolid", "Default");
            int n = Physics.OverlapSphereNonAlloc(prey.transform.position, 25f, doorHits, mask, QueryTriggerInteraction.Collide);
            Vector3 pos = transform.position, preyPos = prey.transform.position;
            Door best = null;
            float bestCost = float.MaxValue;
            for (int i = 0; i < n; i++)
            {
                Door d = doorHits[i] ? doorHits[i].GetComponentInParent<Door>() : null;
                if (!d || d == lastFailedDoor || d.m_keyItem || !d.m_nview || !d.m_nview.IsValid()) continue;
                float cost = Vector3.Distance(pos, d.transform.position) + Vector3.Distance(d.transform.position, preyPos);
                if (cost < bestCost)
                {
                    bestCost = cost;
                    best = d;
                }
            }
            if (!best) return false;

            // Stand on our side of the doorway, then come out on the other side.
            Vector3 fwd = best.transform.forward;
            fwd.y = 0f;
            fwd.Normalize();
            float side = Vector3.Dot(pos - best.transform.position, fwd) >= 0f ? 1f : -1f;
            doorOutside = best.transform.position + fwd * side * 1.8f;
            doorInside = best.transform.position - fwd * side * 2.2f;
            door = best;
            doorStep = DoorStep.Approach;
            doorStepTime = 0f;
            HufsaPlugin.Log.LogInfo("Hufsa is going for the door");
            return true;
        }

        /// <summary>Walks up to the door, opens it if it's shut, and squeezes through after its prey.</summary>
        private void UseDoor(Player prey, float dt)
        {
            if (!door || !door.m_nview || !door.m_nview.IsValid())
            {
                EndDoor(false);
                return;
            }
            doorStepTime += dt;
            Vector3 pos = transform.position;
            Vector3 flatPos = new Vector3(pos.x, 0f, pos.z);

            switch (doorStep)
            {
                case DoorStep.Approach:
                    SetSpeed(HufsaConfig.HunterSpeed.Value * 1.3f);
                    ai.MoveTo(dt, doorOutside, 0.6f, false);
                    if (Vector3.Distance(flatPos, new Vector3(doorOutside.x, 0f, doorOutside.z)) < 1.4f)
                    {
                        doorStep = DoorStep.Open;
                        doorStepTime = 0f;
                        doorPushed = false;
                    }
                    else if (doorStepTime > 25f)
                    {
                        EndDoor(true); // couldn't get to it
                    }
                    break;

                case DoorStep.Open:
                    ai.StopMoving();
                    character.SetLookDir((door.transform.position - pos).normalized, 0f);
                    // Push it once (a second push would swing it shut again before the first one lands).
                    if (!doorPushed && doorStepTime > 0.3f)
                    {
                        doorPushed = true;
                        if (door.m_nview.GetZDO().GetInt("state", 0) == 0)
                            door.Open(door.transform.position - pos); // same as a player pushing it open
                    }
                    if (doorStepTime > 1.2f)
                    {
                        // Squeeze: its body is wider than a doorway, so it narrows while it passes through.
                        var capsule = body as CapsuleCollider;
                        if (capsule)
                        {
                            if (normalRadius < 0f) normalRadius = capsule.radius;
                            capsule.radius = 0.45f;
                        }
                        foreach (Collider c in door.GetComponentsInChildren<Collider>()) Physics.IgnoreCollision(body, c, true);
                        doorStep = DoorStep.Through;
                        doorStepTime = 0f;
                    }
                    break;

                case DoorStep.Through:
                    SetSpeed(HufsaConfig.HunterSpeed.Value);
                    Vector3 to = doorInside - pos;
                    to.y = 0f;
                    if (to.magnitude < 0.8f || doorStepTime > 6f)
                    {
                        EndDoor(to.magnitude >= 0.8f);
                        return;
                    }
                    character.SetMoveDir(to.normalized);
                    character.SetLookDir(to.normalized, 0f);
                    break;
            }
        }

        private void EndDoor(bool failed)
        {
            if (failed) lastFailedDoor = door;
            door = null;
            var capsule = body as CapsuleCollider;
            if (capsule && normalRadius > 0f) capsule.radius = normalRadius;
            normalRadius = -1f;
            noProgress = 0f;
            stuckTimer = 0f;
            lastProgressPos = transform.position;
        }

        private void SetSpeed(float speed)
        {
            character.m_speed = speed;
            character.m_walkSpeed = speed;
            character.m_runSpeed = speed;
        }

        /// <summary>It stops at the sea shore: the ocean and boats are a way out.</summary>
        private static bool OceanAhead(Vector3 pos, Vector3 dir)
        {
            Vector3 ahead = pos + dir * 2.5f;
            return Wet(ahead) && HufsaEntity.BiomeAt(ahead) == Heightmap.Biome.Ocean;
        }

        /// <summary>Water deeper than ankle height here.</summary>
        private static bool Wet(Vector3 p) =>
            ZoneSystem.instance.GetGroundHeight(p, out float h) && h < ZoneSystem.instance.m_waterLevel - 0.4f;

        private bool gliding;

        /// <summary>
        /// Across a river or lake: physics off, it glides on toward its prey at the chase speed with its hem just
        /// in the water, following the ground again as it comes ashore.
        /// </summary>
        private void GlideOverWater(Vector3 dir, float dt)
        {
            if (!gliding)
            {
                gliding = true;
                ai.StopMoving();
                character.SetMoveDir(Vector3.zero);
                if (character.m_body) character.m_body.isKinematic = true;
            }
            if (dir.sqrMagnitude > 0.01f) character.SetLookDir(dir, 0f);
            Vector3 pos = transform.position;
            Vector3 next = pos + dir * chaseSpeed * HufsaConfig.WaterSpeedFactor.Value * dt; // slower on water
            float ground = ZoneSystem.instance.GetGroundHeight(next, out float h) ? h : pos.y;
            float y = Mathf.Max(ground, ZoneSystem.instance.m_waterLevel - 0.3f);
            next.y = Mathf.MoveTowards(pos.y, y, 3f * dt);
            if (character.m_body) character.m_body.position = next;
            transform.position = next;
        }

        private void EndGlide()
        {
            if (!gliding) return;
            gliding = false;
            if (character.m_body)
            {
                character.m_body.isKinematic = false;
                character.m_body.linearVelocity = Vector3.zero;
            }
        }

        /// <summary>Turns off collision with natural obstacles nearby, but never with player builds or ruins.</summary>
        private void IgnoreNatureAround(Vector3 pos)
        {
            if (!body) return;
            int n = Physics.OverlapSphereNonAlloc(pos + Vector3.up * 1.4f, 3.5f, nearby, scanMask, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++)
            {
                Collider c = nearby[i];
                if (!c || c == body || c.GetComponentInParent<Piece>() || c.GetComponentInParent<Character>()) continue;
                if (IsNature(c)) Physics.IgnoreCollision(body, c, true);
            }
        }

        private static bool IsNature(Collider c) =>
            c.GetComponentInParent<TreeBase>() || c.GetComponentInParent<TreeLog>() ||
            c.GetComponentInParent<Destructible>() || c.GetComponentInParent<MineRock>() ||
            c.GetComponentInParent<MineRock5>() || c.GetComponentInParent<Pickable>();
    }
}
