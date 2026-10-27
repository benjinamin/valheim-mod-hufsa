Put Hufsa's sounds here (.ogg, .wav or .mp3). The build copies them to
BepInEx\plugins\Hufsa\sounds. The file name decides what a sound is for
(an optional "Hufsa " in front is ignored, so "Hufsa step 3.mp3" is a step):


  voice*   Hufsa's voice. Each time it "speaks", one is picked at random.
           Hunter: once when it arrives, then every 14-30 s while you're within 70 m.
           The Watcher never makes a sound.
  step*    ONE slow footstep in snow (short, ~0.5-1 s). Played once per stride while
           the Hunter glides; faster when it speeds up.
  creak*   a tree cracking / wood creaking. Played every 7-16 s while it moves.

Several files per group is better (step1.ogg, step2.ogg, ...): it picks one at random
and varies the pitch a little, so it doesn't sound repetitive.

Tips: mono, cut tight (no silence at the start), roughly the same loudness across files.
.ogg is the safest format for Unity.
