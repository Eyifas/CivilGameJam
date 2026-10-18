=== AUDIO MANAGER CUSTOM ASSETS GUIDE ===

You can add your own custom audio files (.wav, .mp3, .ogg) in two easy ways:

----------------------------------------------------------------------
METHOD 1: Place Files in Resources (Recommended - Zero Setup)
----------------------------------------------------------------------
Simply drag and drop your audio files into these folders:

1. Background Music:
   Folder: Assets/Resources/Audio/BGM/
   File names to match:
   - Menu.mp3 / Menu.wav                -> Start menu screen
   - Stage1_Preparation.mp3             -> 60-second preparation phase
   - Earthquake.mp3                     -> Earthquake tremor phase
   - Stage3_PostQuake.mp3               -> Utilities / dark apartment
   - MinigameFocus.mp3                  -> Bookshelf, drawer, table scenes
   - MinigameTension.mp3                -> Cupboard, wardrobe scenes
   - VictoryOutro.mp3                   -> Final assembly area victory

2. Sound Effects:
   Folder: Assets/Resources/Audio/SFX/
   File names to match:
   - Footstep.wav / Footstep.mp3        -> Character footsteps (or Footstep_L / Footstep_R)
   - FlashlightOn.wav                   -> Flashlight click on
   - FlashlightOff.wav                  -> Flashlight click off
   - LightSwitch.wav                    -> Wall switch click
   - ItemPickup.wav                     -> Picking up supplies
   - HammerHit.wav                      -> Bookshelf hammering
   - AnchorSuccess.wav                  -> Anchoring completed fanfare
   - DrawerOpen.wav / DrawerClose.wav   -> Drawer sliding
   - CandlePuff.wav                     -> Candle blown out
   - WardrobeCrash.wav                  -> Falling dolap / heavy impact
   - CabinetCrash.wav                   -> Kitchen cabinet items falling
   - Explosion.wav                      -> Gas switch explosion
   - DoorOpen.wav / DoorLocked.wav      -> Apartment exit door
   - ValveTurn.wav / ValveLatch.wav     -> Gas and water valve turns
   - UIHover.wav / UIClick.wav          -> Button hover & click
   - QTEHit.wav / QTEMiss.wav           -> QTE prompts
   - GasHiss.wav                        -> Leaking gas ambient loop
   - EarthquakeRumble.wav               -> Deep tremor sub-bass rumble

* Note: If any file is missing, the game automatically synthesizes a high-quality procedural fallback.

----------------------------------------------------------------------
METHOD 2: Play Any Custom Audio via Script
----------------------------------------------------------------------
You can call these static functions from any C# script:

// 1. Play by resource file name (placed anywhere in Assets/Resources/Audio/):
AudioManager.PlaySound("MyExplosionSound");

// 2. Play any AudioClip variable:
AudioManager.PlayClip(myAudioClip);

// 3. Override a built-in sound dynamically at runtime:
AudioManager.SetCustomSFX(SFXType.Explosion, myExplosionClip);
AudioManager.SetCustomBGM(BgmTrack.Menu, myMenuMusicClip);
