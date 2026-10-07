# Tornado Chaser 2.0

A mobile infinite runner where you drive a storm-chasing car after a tornado. You have to stay close enough to keep up with it, but not so close that it pulls you in, while dodging the debris it throws at you.

## North Star

Combine the "one more run" feel of an infinite runner with the thrill of chasing a storm. The core hook is the **sweet spot**: getting closer to the tornado scores more but is more dangerous, so every second is a choice between greed and survival.

## Design Pillars

1. **Risk is rewarded.** Getting closer always pays off more, and playing it safe is always worth less.
2. **Every death is fair.** Lethal threats must be easy to read and give a warning in time. A player should always know why they lost.
3. **Instant retry.** Runs are short, and it takes one tap to go from game over to driving again.
4. **The storm is the star.** The tornado should feel huge, alive, and threatening.

## Core Gameplay

- **View:** 3D chase camera behind the car. The tornado looms ahead on the road.
- **Controls:** a one-thumb floating joystick, centered wherever the thumb lands. Drag left or right to steer, up to accelerate, and down to brake. Releasing lets the car coast slower.
- **Speed:** the car drives forward on its own. The tornado's speed changes unpredictably, so the player has to keep adjusting.
- **Chase zones** (by distance from the tornado):

  | Lost | Safe | Close | Danger | Sucked up |
  |------|------|-------|--------|-----------|
  | fail | x1   | x2    | x4     | fail      |

  - **Sucked up:** entering the innermost zone ends the run immediately.
  - **Lost:** falling too far behind starts a short "losing it" warning. The run ends if the player doesn't close the gap before it runs out.
- **Debris:**
  - **Light** (branches, signs, trash cans, hay bales): slows the car down, which can cost the chase. Never lethal by itself.
  - **Heavy** (cars, trees, roofs, farm equipment): kills on contact. It must look different at a glance and be telegraphed, for example with a shadow or marker where it will land.
- **Scoring:** points for every second of chasing × the current zone multiplier, plus a bonus for near-misses with debris.
- **Difficulty:** the tornado's speed swings, debris density, and the share of heavy debris all increase the longer a run lasts.

## Setting and Art Direction

- An endless, procedurally generated rural highway through Plains farmland: fields, barns, silos, power lines.
- **Simple 3D models, realistic atmosphere.** Keep geometry and textures cheap, and get the drama from lighting, fog, rain, dust, lightning, and an animated funnel cloud.
- Debris needs rim lighting or an outline so it stands out against the dark storm.

## Platform and Tech

- **Engine:** Unity 6 (6000.6.4f1), URP with the mobile render pipeline asset (`Assets/Settings/Mobile_RPAsset.asset`), and the new Input System.
- **Target:** iOS and Android phones, portrait orientation, one-handed play.
- **Performance:** keep a steady frame rate on mid-range phones. Performance comes before visual detail.

## Progression (planned)

- Coins earned during runs unlock and upgrade cars (handling, acceleration, top speed).
- Missions and challenges, such as "spend 30s in the Danger zone" or "dodge 10 cars."
- Leaderboards.

## Out of Scope (for now)

- Tornado variants (fire tornado and others) and multiple levels or biomes.
- Monetization. Don't build it yet, but don't design anything that would make adding rewarded ads or in-app purchases hard later.

## Repo Notes

- Commit `Assets/`, `Packages/`, and `ProjectSettings/`. Everything Unity generates is in `.gitignore`.
- Always commit `.meta` files together with their assets.
