# Changelog

## 0.5.0

Watching
- No more counting sightings. Instead a hidden stare meter fills while you look at a Watcher: fast with it in the middle of your view, slowly with it elsewhere on screen, not at all while you're busy (fighting, building, swinging, in a menu). It drains when you look away.
- Stare past its limit and it comes for you: its eyes flare, a sting plays, and a few seconds later it hunts you. Each Watcher's limit is random (4-10 s), and shorter after every hunt you've had.
- The very first Watcher you notice never hunts you; stare at it for about 3 s and it fades away.
- Looking at it no longer makes it vanish. Walking toward it makes it back away into the dark; come too close and it's gone.
- No feedback in text any more: you hear breathing rise as you stare, and its eyes brighten. A low drone comes from the Watcher, louder the closer it is and the more of it you can see.
- Fading away, the glow along its outline goes first and the body follows, cross-faded, so it no longer leaves a lit shape behind (`FadeSeconds`, `FadeGlowFirst`).

Night hunts
- Once you've been hunted, it may come on any night even if you never looked: 5%, +8% for each night since the last hunt, +15% on a night you noticed one and looked away, at most 50%. After any hunt, 2 nights of rest. One night hunt per night across all players.
- The drone and a faint breath come about 20 s before it does, and the drone stays through the hunt.
- The Hunter no longer rushes at you when it appears: its speed eases in over 3 s to the chase speed for its distance (fast from far away, a walk up close) (`StartEaseSeconds`; the Rush settings are gone).
- The Hunter's music starts when it starts moving, not when it appears.

## 0.4.0

- Whoever Hufsa catches is left behind frozen solid: a statue of them cowering, glazed in ice, with icy spikes jutting out of their back. It stays in the world for good, for everyone, until someone breaks it; then it shatters into the game's own Ice (`FrozenVictims`, `FrozenVictimHealth`). It stands 1.5 m ahead of where they fell, clear of their tombstone (`FrozenVictimAhead`).

## 0.3.0

Spotting
- Out of combat a Watcher counts as seen anywhere on screen (enough of it must still show), and it fades away faster after. In combat you still have to look straight at it.

Hunting
- The chill is a little gentler: less slowing (60% at point blank, was 70%) and less stamina drain.
- Hufsa sticks to the player it came for. It only turns to someone else when they die, use a portal, hide in shelter, or it has been stuck too long. Standing still, it jerks round to face the new one and stares a moment before it comes. It turns faster while gliding.
- Tamed creatures and summons that attack it now get hurt when they come close.
- Stuck behind a building between it and a player out in the open, it passes through the walls after a few seconds (never into a shelter its prey hides in).
- Fixed: after a kill, a fresh hunt could start on the survivors right after they portalled home. Once Hufsa has killed, whoever gets away is let go.
- Hufsa gives up on a player in shelter sooner: 25 s (was 60), or 5 s once it is right at the door (was 10).
- Hufsa no longer changes the weather at all (no more forced snow near the Hunter or fog with the Watcher), in any biome.
- The Hunter's music now plays on the music channel (follows your music volume). While it plays, the biome's music fades out, and it fades back in once Hufsa's music has faded away.
