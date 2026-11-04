# Horror "stare" mechanics, dread meters and pacing directors: research for Hufsa

Researched 2026-11-04. Scope: how horror games detect "looking at it", escalate, give feedback, pace threats, and keep unprovoked threats fair. Used to inform the Watcher (distant silhouette with glowing eyes) and the Hunter in this mod.

**Source quality key**
- **[Primary]**: developer talk/slides, a developer-written article, or the game's own source code.
- **[Dev-based secondary]**: journalist write-up built on a named developer talk or interview.
- **[Fan wiki]** / **[Community]**: fan wiki, guide site, mod page or forum. Treat the numbers as unverified.

Several fan wikis (Fandom, wiki.gg mirrors) refused automated fetches (HTTP 402/401/bot wall). Where the details below come only from search snippets of those pages, they are marked **[Fan wiki, unverified]**.

---

## 1. "Looking at it too long triggers something"

### 1.1 SCP – Containment Breach (SCP-173 and SCP-096): exact source code

The game's Blitz3D source is public ([Regalis11/scpcb](https://github.com/Regalis11/scpcb)). Time is in ticks of 1/70 s (`FPSfactor`; `70*6` = 6 s). **[Primary]**

**SCP-173 ("moves only when unseen")**: [NPCs.bb L683–760](https://github.com/Regalis11/scpcb/blob/master/NPCs.bb#L683)
- **Detection.** The view check only runs within 15 units. `EntityInView(n\obj, Camera)` is a **camera-frustum test** with no occlusion check. If the player is not mid-blink, 173 is "seen" and cannot move. `EntityVisible` (a line-of-sight raycast) is used separately, within 10 units, to track the player and to gate the scare stings.
- **Blink as forced breaks in looking.** The blink timer counts down at 0.6×/tick from `BLINKFREQ`, which is `Rnd(490,700)` on Easy, `Rnd(455,665)` on Normal and `Rnd(420,630)` on Hard, so a blink comes every ~10–17 s. The eyes then close for 20 ticks (~0.3 s), and 173 may move during ticks −6 to −16 (~0.14 s) ([Main.bb ~L3048–3075](https://github.com/Regalis11/scpcb/blob/master/Main.bb#L3048)). Players can blink by hand, and holding the key keeps the eyes shut ([Main.bb L4349–4350](https://github.com/Regalis11/scpcb/blob/master/Main.bb#L4349)).
- **Feedback while looking** scales with distance:
  - Blur: `(4 - dist)/6`, clamped 0.1–0.9.
  - Camera zoom pulse: a sine wave × `(3.5 - dist)/3.5`.
  - Horror sting when within 3.5 units, **at most once per 60 s** (`MilliSecs2() - n\LastSeen > 60000`).
  - When it closes from >2 to <1.5 units: a hard zoom, heartbeat 140 bpm, and one of 5 random stings.
  - A random scrape sound at a 1/700 chance per tick.
  - Its stone-drag sound has volume proportional to how far it moved.
- **Off-screen escalation.**
  - More than 50 units away: 1/70 chance per tick to teleport to a waypoint 15–25 units from the player.
  - Mid-range: random waypoint hops (`TeleportCloser`).
  - Near: active pursuit, including opening doors.

**SCP-096 ("seeing its face")**: [NPCs.bb L1242–1300](https://github.com/Regalis11/scpcb/blob/master/NPCs.bb#L1242)
- **Gate.** Only within 8 units, while idle (State 0). Its idle music starts streaming at this distance, which serves as a warning.
- **Detection, in order:**
  1. **Facing angle:** `DeltaYaw(096 → player)` must be within ±90°, so the player is in front of its face.
  2. **Screen projection:** `CameraProject` of a point 0.25 above its origin must land inside the screen rectangle.
  3. **Line of sight:** `EntityVisible(player, 096)`.
  4. **Not blinking.**

  The trigger is **instant**. There is no dwell time.
- **Escalation.**
  - On trigger: a "Triggered" sting, a camera zoom, and State 1.
  - The wind-up/panic phase lasts **18 s** (`State2 > 70*18`, ~L1536) before the charge (State 4).
  - During the charge it rechecks line of sight every 3 s, repaths every 5 s, and teleports closer if more than 32 units away.
- **Lesson.** The 18 s wind-up after an instant trigger turns the trigger into dread rather than a cheap death.

### 1.2 Slender: The Eight Pages (Parsec, 2012) and Slender: The Arrival (Blue Isle/Parsec, 2013)
- **Eight Pages.** Looking at Slender Man too long ends the game, with static as the feedback ([Giant Bomb](https://www.giantbomb.com/games/3030-39028/), [Wikipedia mirror](https://ipfs.ssi.eecc.de/ipns/en.wikipedia-on-ipfs.org/wiki/Slender%3A_The_Eight_Pages)). **[Secondary]**
  - The game-over screen is his face up close with pulsing static.
  - Turning the flashlight off lengthens how long you can look before losing, but makes it easier for him to find you ([Giant Bomb](https://www.giantbomb.com/games/3030-39028/)).
  - Changelogs say the player's jog speed rises as pages are collected, and a v0.9.1 readme mentions a slight increase in the enemy's speed. This is page-count escalation ([Softpedia changelog](https://mac.softpedia.com/progChangelog/Parsec-Slender-Changelog-120489.html), [readme on Scribd](https://www.scribd.com/doc/212937582/Readme)). **[Secondary]**
  - Exact thresholds, and whether proximity feeds the static, were **not found in any primary source**.
- **The Arrival.** Looking at him distorts the UI with static, blur and colour spots, plus audio distortion. The effects last until you face away. The camera "picks up interference" from him ([Wikipedia](https://en.wikipedia.org/wiki/Slender:_The_Arrival)). **[Secondary]**
  - The fan-wiki claim that you cannot pause during the static ("to prevent them taking a break") is **[Fan wiki, unverified]** ([TV Tropes](https://tvtropes.org/pmwiki/pmwiki.php/Videogame/SlenderTheArrival)).
  - No developer source with numeric thresholds was found. The interviews with lead designer Alex Tintor ([Escapist](https://www.escapistmagazine.com/?p=89699)) and with Blue Isle ([Rely on Horror](https://www.relyonhorror.com/in-depth/interview-blue-isle-studios-talks-about-the-past-and-future-of-slender-the-arrival)) could not be read in full.
- **Design read:** static works as a meter with no UI. It is diegetic (the camera), it is continuous, and it tells you two things at once: it is here, and stop looking.

### 1.3 Amnesia: The Dark Descent (Frictional, 2010)
- Thomas Grip (creative director) wrote a deep dive on the sanity meter ([Game Developer, 2014](https://www.gamedeveloper.com/design/game-design-deep-dive-i-amnesia-i-s-sanity-meter-)). **[Primary]**
  - **Drains:** sanity is lost "from seeing monsters, from witnessing horrific events, and … being in the dark". Scripted "insanity drops" are attached to events such as a door swinging open.
  - **Feedback:** screen effects tuned to "annoying-but-not-too-annoying" (pulled back so players did not feel ill), a pulse on each drop, a cracking-eggshell audio loop that worsens the longer sanity stays low, and a text/brain image in the inventory.
  - **Problems:**
    - Once levels were interconnected, sanity could not be reset between them. For many players it either barely mattered or left them stuck low, which made the game "almost unplayable".
    - It depended on too many variables to tune, monitor brightness among them.
    - Players hoarded oil.
    - Some players removed the audio cue with mods.
  - **Lesson:** removing the challenge made sanity ambient, which improved the experience. In Grip's words, "making something game-y just destroys the feeling". Vagueness made players invent their own theories, which raised their fear.
- The article does not describe how "looking at a monster" is detected.
- **Amnesia: The Bunker / Rebirth.** Rebirth replaced sanity with a fear/illness system ([PC Gamer](https://pcgamer.com/amnesia-rebirth-trades-sanity-for-a-strange-illness-inspired-by-soma)). No primary source on a look-based mechanic in The Bunker was found.

### 1.4 Lethal Company (Zeekerss): Bracken and Coil-Head
No developer documentation exists. The decompiled values circulate only through mods and fan wikis, and the wikis blocked fetching.
- **Bracken ("Flowerman")** **[Community]**
  - A mod that exposes its internal AI state ([BrackenAngerMeter](https://thunderstore.io/c/lethal-company/p/azumi/BrackenAngerMeter/)) describes:
    - **States:** SNEAKING (stalks out of sight), EVADING (recently observed, so it retreats) and ANGRY.
    - **Anger meter:** about 0–12. It rises when players observe or threaten the Bracken and decays over time.
    - **Threat stacks:** a counter (`timesThreatened`) rises when it turns ANGRY while anger is still low. On entering ANGRY, `timesThreatened/1.75` is added to anger, which **lengthens later anger periods**. The Bracken "remembers".
  - Guides say staring for consecutive seconds angers it ([PC Invasion](https://www.pcinvasion.com/how-to-avoid-the-shadow-man-in-lethal-company/)), and that it drops back to sneaking about 24 s after triggering ([The Nerd Stash](https://thenerdstash.com/how-to-survive-flower-man-bracken-in-lethal-company)). **[Community, unverified]**
  - **Design read:** the **first response to being looked at is retreat, not attack**. Anger accumulates across repeated looks.
- **Coil-Head.** It moves very fast when no one looks at it and freezes on eye contact, a homage to SCP-173 ([Prima Games](https://primagames.com/?p=207390), [GGRecon](https://www.ggrecon.com/guides/all-lethal-company-monsters-and-how-to-avoid/)). **[Community, unverified]**
  - Whether the check is a view cone or line of sight, and its rest periods, could not be verified from code.

### 1.5 Dead by Daylight: The Shape (Michael Myers), "Evil Within"
- **Original version (2016–2025).** Stalking fills tiers I→II→III. Tier I is Undetectable, with a small effective terror radius. Tier II has a 16 m terror radius and Tier III 32 m. **Tier III lasts 60 s and then drops back to Tier I** ([BHVR forum guide via search](https://forums.bhvr.com/dead-by-daylight/discussion/423737/reworking-the-shape-to-help-improve-performance-and-gameplay)). Each tier needs 5 stalk points, and each survivor holds a limited pool of "evil" ([DbD wiki](https://deadbydaylight.fandom.com/wiki/Halloween)). **[Fan wiki, unverified]**
  - Wikis disagree on the Tier I radius (0 / 6 / 8 m).
- **Current version (Sinister Grace rework, 2025).** It has Stalker, Pursuer and Evil Incarnate modes ([community wiki](https://deadbydaylight.wiki.gg/wiki/Evil_Within)). **[Fan wiki]**
  - Holding the power stalks survivors **2.5–32 m away**. They are outlined, and the closest one is drained.
  - Moving while stalking costs 25% of the stalk rate.
  - Once the gauge is not full, it **starts emptying after 20 s without stalking, down to a 50% floor**.
  - Evil Incarnate lasts 60 s on that page; patch-note coverage says 40 s ([MP1st](https://mp1st.com/title-updates-and-patches/dead-by-daylight-sinister-grace-update-patch-9-200-001-3-56)).
- **Design read:** this inverts Hufsa's case, since the *monster* stares at the *player*. Still useful:
  - A **range window** (not too close, not too far).
  - **Decay with a floor**, so progress is never fully lost.
  - A **timed peak** that falls back to the low tier.

### 1.6 Phasmophobia (Kinetic Games): sanity-gated hunts **[Fan wiki / Community, unverified]**
- **Hunt gate:** hunts become possible when average sanity is below a threshold. The default is commonly 50%; some ghosts differ, for example Demon at 70% and Mare at 60% in the dark ([Fandom Hunt page via search](https://phasmophobia.fandom.com/wiki/Hunt), [Steam discussion](https://steamcommunity.com/app/739630/discussions/0/3816290897719856608)).
- **Grace period** at the start of a hunt, while the ghost cannot see players: about 5/4/3/2 s by difficulty (forum figures).
- **Hunt duration:** about 15–40 s on Amateur up to 30–60 s on Professional/Nightmare, depending on map size. Cursed hunts add 20 s.
- **Cooldown:** **at least 25 s between hunts**, with no guarantee of a hunt when it ends.
- **Design read:**
  - A **pressure stat** (sanity) only *unlocks* the chance of attack and does not schedule it.
  - A **grace period** gives fairness at the start of an attack.
  - A **minimum cooldown** gives fairness between attacks.

### 1.7 Others asked about
- **Escape the Backrooms.** Guides list "eye contact" as one of the game's core monster rules, and describe a short window to move around the Smiler while lit ([playnews.gg bestiary](https://www.playnews.gg/en/guides/escape-the-backrooms-complete-2026-bestiary-guide-all-entities-and-how-to-dodge-them), [Destructoid](https://www.destructoid.com/all-monsters-and-levels-in-escape-the-backrooms-entities-guide/)). **[Community, unverified]** No code-level detail was found.
- **Darkwood (Acid Wizard).** It uses a narrow view cone with blacked-out peripheral vision, and shows things moving at a distance without revealing what caused them. The developers call jump scares shallow: "seeing (or hearing) objects move without knowing what caused the motion … was eerily appealing to us" (Gustaw Stachaszewski, [Game Developer, 2019](https://www.gamedeveloper.com/design/how-i-darkwood-i-s-visibility-mechanics-create-a-new-kind-of-horror)). **[Dev-based secondary]**
  - Its nights mix random and scripted events, and some nights nothing happens but creaking ([Wikipedia](https://en.wikipedia.org/wiki/Darkwood), [Destructoid](https://www.destructoid.com/review-in-progress-darkwood/)). **[Secondary]**
- **Mouthwashing, Doki Doki Literature Club.** No documented stare or look-detection mechanic was found in any source. Their horror is narrative and meta, so they are not covered here.

---

## 2. Dread meters without a visible bar

| Game | Hidden stat | Feedback channels | Source |
|---|---|---|---|
| SCP-CB | distance to 173 while seen | blur, zoom pulse, heartbeat, stings (rate-limited to 1 per 60 s) | [code](https://github.com/Regalis11/scpcb/blob/master/NPCs.bb#L683) |
| Slender | look time (+ flashlight) | screen and audio static | [Wikipedia](https://en.wikipedia.org/wiki/Slender:_The_Arrival) |
| Amnesia | sanity | screen warp, pulse on drop, worsening audio loop, inventory text | [Grip](https://www.gamedeveloper.com/design/game-design-deep-dive-i-amnesia-i-s-sanity-meter-) |
| Alien: Isolation | menace gauge (AI-only) | the alien's behaviour itself: it leaves into the vents, motion-tracker pings | [Game Developer](https://www.gamedeveloper.com/design/the-perfect-organism-the-ai-of-alien-isolation) |
| L4D | survivor intensity (AI-only) | the threat population; nothing is shown to the player | [Booth slides](https://steamcdn-a.akamaihd.net/apps/valve/2009/ai_systems_of_l4d_mike_booth.pdf) |

How they stay readable without a bar:
- **Monotonic, layered cues.** One continuous channel (static or blur strength) shows the level, and discrete stings mark crossings (SCP-CB).
- **Diegetic sources:** the camera, the eyes, the heartbeat.
- **Rate-limit the stings** (SCP-CB: 60 s) so they don't become noise.
- **Tune for "annoying-but-not-too-annoying"**, or players mod the cue out (Grip).
- Keep **some vagueness**. Per Grip, players' theories about the system scared them more than precise knowledge of it.

---

## 3. Jump scares vs slow-burn dread; director systems

### 3.1 Left 4 Dead AI Director: Mike Booth, "The AI Systems of Left 4 Dead" (2009) **[Primary]**
[Slides (PDF)](https://steamcdn-a.akamaihd.net/apps/valve/2009/ai_systems_of_l4d_mike_booth.pdf), pp. 78–82:
- **Motivation.** Drawn from Counter-Strike's "spiky" pacing:
  - "Constant, unchanging combat is fatiguing."
  - "Long periods of inactivity are boring."
  - "Unpredictable peaks and valleys of intensity create a powerfully compelling and replayable experience."
- **Survivor Intensity** is one number per player; the Director uses the max over the team. It **rises** with damage taken, being incapacitated, being pulled or pushed off a ledge, and nearby kills (inversely with distance). It **decays toward zero over time**, but **not while enemies are actively engaging**.
- **Cycle:**
  - **Build Up:** full threat population until intensity crosses the peak threshold.
  - **Sustain Peak:** keep going for **3–5 s** more.
  - **Peak Fade:** switch to minimal threats and wait until intensity decays out of the peak range, so the fight can finish without using up the Relax period.
  - **Relax:** minimal threats for **30–45 s**, or until the team has travelled far enough, then Build Up again.
- **Boss encounters ignore adaptive pacing.** Without them the pacing suffers too much, and they are meant to break up the rhythm anyway.

### 3.2 Alien: Isolation: two brains and the menace gauge **[Dev-based secondary]**
Tommy Thompson's write-up, based on Andy Bray's nucl.ai 2016 talk and Q&A ([Game Developer](https://www.gamedeveloper.com/design/the-perfect-organism-the-ai-of-alien-isolation)):
- **Two brains:**
  - The **Director** always knows where the player is. It periodically gives the alien a *hint* of an area, never the exact position.
  - The **alien** has to find the player with its own senses, which keeps it fair and lets the player fool it.
- **Menace gauge.** It rises when the alien is a short walk away, when the player has line of sight to it, and when it is close on the motion tracker and could reach the player quickly. It rises more slowly if the alien is close but in another room. **"Menace comes not from attacking the player, but being in their proximity."**
- **At peak, or after too long at peak,** the Director pulls the alien backstage into the vents, toward vents ahead of the player's likely route. This is the relax phase.
- **Fairness:** it teleports only twice in the campaign, both times for cutscenes. Designers can mark areas the alien never searches, to prevent unfair ambushes.
- **Against predictability:**
  - A behaviour tree of 100+ nodes, with locked branches that unlock as the player progresses (never on player deaths, which would feel unfair), so the alien seems to learn.
  - Deliberately suboptimal, backtracking search paths and "spot" pauses that read as doubt.

### 3.3 Jump scares
- Grip: the most important part of jump scares is the *threat* of them. Overuse makes players used to them and the effect wears off ([PC Gamer](https://pcgamer.com/what-horror-game-creators-think-about-jump-scares), quoted via search; the page could not be read in full). **[Dev-based secondary]**
- Darkwood's developers avoid jump scares in favour of unexplained movement and sound ([Game Developer](https://www.gamedeveloper.com/design/how-i-darkwood-i-s-visibility-mechanics-create-a-new-kind-of-horror)).
- SCP-CB in code: the close-range sting picks one of 5 at random, the proximity sting is rate-limited to 60 s, and an ambient sting rolls 1/700 per tick. These are **variety** and **rate limits** as tools against desensitisation.

---

## 4. Never fully safe, but fair

Patterns found:
1. **Pressure unlocks the chance; it doesn't schedule the event.** Phasmophobia's sanity threshold makes a hunt *possible*, then it is rolled and gated by a cooldown.
2. **Mandatory relax after a peak.** L4D Relax lasts 30–45 s. Alien: Isolation sends the alien to the vents when menace peaks.
3. **Grace windows at the start of an attack.** Phasmophobia gives 2–5 s. SCP-096 has an 18 s wind-up after the trigger.
4. **Hard minimum cooldowns.** Phasmophobia leaves 25 s+ between hunts. SCP-CB stings are capped at 1 per 60 s.
5. **Decay with a floor, and memory.** DbD drains only after 20 s, down to 50%. Lethal Company's Bracken threat stacks make each later anger longer.
6. **Imperfect information for the monster.** Alien: Isolation gives hints, not positions, and has no cheating teleports.
7. **Some quiet nights are part of the design.** Darkwood nights where only creaking happens keep the threat uncertain without spending it.

Pity timers (guaranteed event after N quiet rolls) were **not documented as such** in any horror source found. They are a general game-design pattern, and the recommendation in §5 is an inference, not a cited practice.

---

## 5. Takeaways for Hufsa

Hufsa currently counts discrete "sightings" (centred, unobstructed, for a moment) and starts a hunt at `SightingsForHunt`. The count decays by one per `DreadDecayDays` quiet days and resets after a hunt (`HufsaDread.cs`). Suggestions build on that.

**Detection (per frame, local client)**
- Use SCP-096-style **layered checks**, cheap first:
  1. Distance window.
  2. Angle from the camera's forward to the eyes.
  3. A raycast to the **eyes**, not the root. 096 projects a point 0.25 above its origin.
- Use a **view cone that narrows with distance**, rather than one fixed angle. Hufsa is far away, so "looking at" should mean the eyes are near the centre of the screen. Suggested ranges:
  - **Glance:** within ~10–12° of centre. Starts the feedback.
  - **Stare:** within ~4–6° of centre. Accumulates stare time.
  - Also count it when the eyes take up a meaningful screen fraction (zoomed or close), so close range isn't impossibly strict.
- **Accumulate continuous stare time, not just events.** Fill a hidden `stare` value while staring, and decay it slowly when not (half-life ~5–10 s), so short repeated peeks still add up.
  - One "sighting" = stare ≥ **1.5–3 s**.
  - "Too long" = **6–10 s** cumulative in one encounter. This triggers an immediate Hunter chase, independent of the nightly sightings count.
- **Occlusion:** trees and foliage should block. Use the raycast, not just a frustum test. Unlike SCP-173's `EntityInView`, Hufsa should not count through walls.
- **Ignore** stare time while a menu or map is open, during sleep, and while the player is dead.

**Feedback (no bar, diegetic, layered like SCP-CB and Slender)**
- **Continuous channel**, proportional to stare: faint vignette and desaturation at the screen edge, slight FOV pull or zoom pulse toward Hufsa (as SCP-CB's 173 does), and a low drone or tinnitus rising in pitch and volume. The eyes glow brighter as stare builds.
- **Discrete stings at thresholds**, for example a sighting registered and the half-way mark: a breath or heartbeat thump. Rate-limit to **1 per 45–60 s** and pick from 3–5 variants at random.
- **Tune to "annoying-but-not-too-annoying"** (Grip). Provide a config to soften the screen effects.
- **Looking away should feel like relief:** effects ease out over 1–2 s, not instantly.
- Keep the existing quiet messages, but don't reveal numbers. Vagueness helps.

**Escalation**
- **Stage 1 (glance):** the Watcher does nothing, or slowly turns its head toward you.
- **Stage 2 (sighting):** like the Bracken, its first response is to **withdraw**: vanish, or appear farther away next time.
- **Stage 3 (stare limit):** a wind-up before the chase, like 096's 18 s but shorter, about **5–10 s**. The Watcher vanishes, a distant sound plays, and then the Hunter spawns at a fair distance (the Hunter never spawns inside the view or within ~40–60 m).
- **Memory:** each stare-triggered hunt slightly shortens the next stare limit (−10–15%, floor ~4 s), like the Bracken's threat stacks. Reset it when a hunt is survived, or over in-game days.

**Unprovoked hunts ("some nights it hunts anyway")**
- Use **dread to unlock, then roll**, like Phasmophobia: each eligible night, roll a hunt with probability `base + k × dread`. Suggested: base **3–5%**, ramping to **25–35%** near the sightings threshold.
- **Cooldown:** at least **2–3 in-game nights** after any hunt, whether triggered or unprovoked. Never two nights in a row.
- **Pity (inference):** after ~**8–12** eligible quiet nights, raise the roll sharply so the threat never becomes purely theoretical. Cap so it is never a certainty, which would make it predictable.
- **Build-up before an unprovoked hunt, like L4D's Build Up:** in the preceding 1–3 minutes, more distant Watcher sightings, wrong sounds and quiet messages. Players should feel it coming without a bar.
- **Relax after any hunt (L4D/Alien):** a guaranteed quiet window, for example the rest of the night plus the next day, with no Watcher spawns. Keep the existing reset of dread to 0.
- **Grace at hunt start:** 3–5 s of wind-up and sound before the Hunter can see or chase you, like Phasmophobia.
- **Fair spawning:** spawn the Hunter out of view, at the edge of draw distance, never inside shelters the player is already in, and give it hints rather than perfect tracking (Alien: Isolation's two brains). The server already knows the target, so the Hunter AI should still have to close the distance.

**Pitfalls to avoid**
- **Instant death or chase on first eye contact.** Players learn never to look, which removes the core Watcher experience. Make looking tempting: the eyes are the reward and the risk.
- **Constant pressure.** "Constant, unchanging combat is fatiguing" (Booth). Long quiet stretches are needed for dread to come back.
- **Over-firing stings.** Rate-limit and vary them, or players get desensitised (Grip; SCP-CB's 60 s limit).
- **Screen effects too strong**, which cause nausea or get modded out (Amnesia).
- **Meters that never reset**, such as Amnesia's interconnected levels leaving players stuck low. Keep decay and reset after a hunt.
- **Detection that feels unfair:** counting a stare through foliage, or while the player is in a menu or map. A frustum-only check (like SCP-173's) counts things behind cover.
- **Predictable schedules**, such as "every third night". Randomise within cooldown and pity bounds, and vary the build-up cues.
- **Multiplayer:** stare and dread are per player, as now. Decide whether one player's stare-triggered hunt pulls in nearby friends; L4D uses the team's max intensity, which may be the more readable choice for co-op.
