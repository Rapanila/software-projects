# Mixamo animations for Nine to Fight

Download the animations below from Mixamo (https://www.mixamo.com, needs a logged-in Adobe account)
and save them, renamed exactly as listed, into:

```
D:\Serban\software-projects\nine-to-fight\Assets\_Project\Art\Animations\Mixamo\
```

Create the folder if it does not exist. Do not put anything else in it.

## Download settings (same for every file)

| Setting | Value |
|---|---|
| Character | Mixamo's default (Y Bot); don't upload a custom character |
| Format | FBX for Unity (.fbx) |
| Skin | Without Skin |
| Frames per second | 30 |
| Keyframe reduction | none |
| In Place | ticked, whenever the animation offers it |
| Mirror | off |
| Other sliders (Overdrive, Character Arm-Space, Trim) | leave at default |

## How to choose

- Type the **search** term into Mixamo's animation search. If several results fit, pick the one that best matches **what it should look like**. Use the **fallback** search if the first finds nothing usable.
- Each file must be a **different** Mixamo animation. Don't use the same animation twice, even under different names.
- Attacks should be done with the **right** hand or **right** leg where there is a choice (the character holds items in the right hand).
- Prefer animations that start and end in a fighting stance, take under ~1.5 s, and stay roughly in place.
- **Required** files matter most. **Optional** ones can be skipped if nothing suitable exists.

## Files

| # | File name | Search | Fallback search | What it should look like | Required |
|---|---|---|---|---|---|
| 1 | fight_idle.fbx | fight idle | boxing idle | Boxing guard, bouncing lightly on the feet, fists up | yes |
| 2 | block_idle.fbx | block idle | center block | Both forearms up in front of the face, holding the block | yes |
| 3 | hit_head.fbx | head hit | hit reaction | Head snaps back from a punch, recovers to a stance | yes |
| 4 | hit_body.fbx | stomach hit | body hit | Doubles over from a body blow, recovers | yes |
| 5 | knockout.fbx | knocked out | falling back death | Falls backwards flat onto the floor | yes |
| 6 | jab.fbx | lead jab | jab | Quick straight punch with the lead (front) hand | yes |
| 7 | cross.fbx | cross punch | punching | Straight power punch with the rear (right) hand | yes |
| 8 | hook.fbx | hook punch | hook | Bent-arm hook swinging in from the side | yes |
| 9 | uppercut.fbx | uppercut | uppercut punch | Dip low, drive the fist up | yes |
| 10 | elbow.fbx | elbow | elbow punch | Horizontal elbow strike at head height | yes |
| 11 | knee.fbx | knee | knee kick | Knee driven up into the opponent (ideally grabbing them) | yes |
| 12 | front_kick.fbx | front kick | push kick | Straight forward push kick with the rear leg | yes |
| 13 | flying_kick.fbx | flying kick | jump kick | Leaping kick, one leg spearing forward in the air | yes |
| 14 | dropkick.fbx | drop kick | jumping two feet kick | Jumps and kicks forward with both feet, lands | yes |
| 15 | superman_punch.fbx | superman punch | jumping punch | Hop forward off the back leg into a straight punch | yes |
| 16 | overhead_chop.fbx | melee attack downward | sword slash downward | One-handed weapon brought straight down from overhead | yes |
| 17 | horizontal_swing.fbx | melee attack horizontal | sword horizontal slash | One-handed weapon swung flat across the body | yes |
| 18 | backhand.fbx | melee attack backhand | backhand | One-handed swing from across the body outwards | yes |
| 19 | diagonal_slash.fbx | sword slash | slash | One-handed diagonal slash from high to low | yes |
| 20 | spin_attack.fbx | melee attack 360 | spin attack | Full-body spin with the weapon out, hitting on the turn | yes |
| 21 | jump_slam.fbx | jump attack | great sword jump attack | Jumps and slams the weapon down into the ground | yes |
| 22 | two_hand_slam.fbx | great sword slash | two handed slash | Two-handed vertical overhead smash | yes |
| 23 | two_hand_cleave.fbx | great sword horizontal | great sword high spin attack | Two-handed horizontal swing across the whole body | yes |
| 24 | golf_swing.fbx | golf drive | golf swing | Golf drive: backswing, low hit, high follow-through | yes |
| 25 | overhand_throw.fbx | throw | baseball pitching | Overhand throw like pitching a ball | yes |
| 26 | underhand_toss.fbx | toss | underhand throw | Underhand fling from low to high | no |
| 27 | shove.fbx | push | shoving | Two-handed push with a step forward | no |
| 28 | lunge_stab.fbx | stab | lunge | Long forward lunge, thrusting one arm straight out | no |
| 29 | megaphone_blast.fbx | magic attack | standing 1h magic attack | Leans back then thrusts one hand forward (casting) | no |
| 30 | ground_pound.fbx | smash | hammer | Short two-handed pound straight down | no |
| 31 | windmill.fbx | melee combo attack | standing melee combo attack | One-handed swings looping round, ending with a fling | no |
| 32 | whip_crack.fbx | slash | sword and shield slash | A one-handed slash different from #16-#19 | no |

## Manifest

When finished, also write `manifest.json` into the same folder: one entry per downloaded file, with the
Mixamo animation name chosen and whether In Place was available. List any skipped files with a reason.

```json
{
  "files": [
    { "file": "fight_idle.fbx", "mixamo_name": "Bouncing Fight Idle", "in_place": false },
    { "file": "underhand_toss.fbx", "skipped": "no underhand throw animation found" }
  ]
}
```

## Machine-readable list

```json
[
  {"file": "fight_idle.fbx", "search": "fight idle", "fallback": "boxing idle", "required": true},
  {"file": "block_idle.fbx", "search": "block idle", "fallback": "center block", "required": true},
  {"file": "hit_head.fbx", "search": "head hit", "fallback": "hit reaction", "required": true},
  {"file": "hit_body.fbx", "search": "stomach hit", "fallback": "body hit", "required": true},
  {"file": "knockout.fbx", "search": "knocked out", "fallback": "falling back death", "required": true},
  {"file": "jab.fbx", "search": "lead jab", "fallback": "jab", "required": true},
  {"file": "cross.fbx", "search": "cross punch", "fallback": "punching", "required": true},
  {"file": "hook.fbx", "search": "hook punch", "fallback": "hook", "required": true},
  {"file": "uppercut.fbx", "search": "uppercut", "fallback": "uppercut punch", "required": true},
  {"file": "elbow.fbx", "search": "elbow", "fallback": "elbow punch", "required": true},
  {"file": "knee.fbx", "search": "knee", "fallback": "knee kick", "required": true},
  {"file": "front_kick.fbx", "search": "front kick", "fallback": "push kick", "required": true},
  {"file": "flying_kick.fbx", "search": "flying kick", "fallback": "jump kick", "required": true},
  {"file": "dropkick.fbx", "search": "drop kick", "fallback": "jumping two feet kick", "required": true},
  {"file": "superman_punch.fbx", "search": "superman punch", "fallback": "jumping punch", "required": true},
  {"file": "overhead_chop.fbx", "search": "melee attack downward", "fallback": "sword slash downward", "required": true},
  {"file": "horizontal_swing.fbx", "search": "melee attack horizontal", "fallback": "sword horizontal slash", "required": true},
  {"file": "backhand.fbx", "search": "melee attack backhand", "fallback": "backhand", "required": true},
  {"file": "diagonal_slash.fbx", "search": "sword slash", "fallback": "slash", "required": true},
  {"file": "spin_attack.fbx", "search": "melee attack 360", "fallback": "spin attack", "required": true},
  {"file": "jump_slam.fbx", "search": "jump attack", "fallback": "great sword jump attack", "required": true},
  {"file": "two_hand_slam.fbx", "search": "great sword slash", "fallback": "two handed slash", "required": true},
  {"file": "two_hand_cleave.fbx", "search": "great sword horizontal", "fallback": "great sword high spin attack", "required": true},
  {"file": "golf_swing.fbx", "search": "golf drive", "fallback": "golf swing", "required": true},
  {"file": "overhand_throw.fbx", "search": "throw", "fallback": "baseball pitching", "required": true},
  {"file": "underhand_toss.fbx", "search": "toss", "fallback": "underhand throw", "required": false},
  {"file": "shove.fbx", "search": "push", "fallback": "shoving", "required": false},
  {"file": "lunge_stab.fbx", "search": "stab", "fallback": "lunge", "required": false},
  {"file": "megaphone_blast.fbx", "search": "magic attack", "fallback": "standing 1h magic attack", "required": false},
  {"file": "ground_pound.fbx", "search": "smash", "fallback": "hammer", "required": false},
  {"file": "windmill.fbx", "search": "melee combo attack", "fallback": "standing melee combo attack", "required": false},
  {"file": "whip_crack.fbx", "search": "slash", "fallback": "sword and shield slash", "required": false}
]
```
