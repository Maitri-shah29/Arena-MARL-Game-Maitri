# ML-Agents Hide and Seek

## ResQ disaster rescue preview

Open `Assets/Scenes/ResQ_Disaster_Scene.unity` in Unity 2021.3.10f1 and press Play. This scene uses the dedicated `Survivor.prefab` and a green Safe Triage Zone trigger in the arena corner. Trapped survivors stay where they are until a drone finds them. A drone locates a survivor by getting within 1.2 metres after the reconnaissance period. That survivor then follows the assigned drone to the zone. The mission succeeds only when every survivor enters it. The HUD reports located and evacuated counts separately.

The drones search with the existing hide-and-seek ONNX checkpoint. When a drone sees a trapped survivor in its sensor cone, the approach assist steers it straight to that survivor, with one drone per survivor. The C# escort state machine then handles evacuation. The assist drives in a straight line, so debris can still stall a drone. Clear the Game Controller's `droneApproachAssist` field to watch the policy alone. Navigating through debris reliably needs a checkpoint trained in the rescue training scene. The `Test` and `Training` scenes retain the original hide-and-seek rules.

### Rescue controls and reports

- In the build phase, choose **Collapsed School**, **Blocked Corridor**, or **Open Rubble Field** from the scenario selector, then press Space to deploy.
- During a mission, press **H** to show or hide the 30 × 30 thermal sweep heatmap. Press **4** for a first-person drone camera; **1–3** select the other camera views.
- Press **B**, or use **Run 10x Stress Test**, to run ten consecutive missions at 3× simulation speed. The panel reports mean time to rescue (MTTR), extraction rate, and sweep coverage per second.
- After missions finish, press **X**, or click **Export Mission Audit**. New mission rows are appended to `DataSet/resq_mission_telemetry.csv` in the repository when running in the editor.

### Train rescue drones

Open `Assets/Scenes/ResQ_Training_Scene.unity` in Unity 2021.3.10f1. Survivors need no checkpoint here: they stay in place while trapped and are steered while escorted. The scene uses the `RescueDrone` behavior for the three drones, has no approach assist so the policy must learn to find survivors, and gives cooperative POCA rewards for first thermal sighting, escort, evacuation, and full mission success. The unrescued survivors impose a small per-step penalty. Start the trainer from the repository root, then press Play in Unity:

```powershell
& "C:\Users\Lenovo\.venvs\arena-marl-game-maitri\Scripts\mlagents-learn.exe" "Unity Project/config/resq_train.yaml" --run-id resq_poca_001
```

After training produces an ONNX checkpoint, assign it to the disaster scene's Game Controller `seekersCheckpoint` field. The original `arena_train.yaml` and `Training` scene remain for hide-and-seek training.

### Optional webcam hand control

The webcam sender uses a separate Python environment so MediaPipe does not change ML-Agents dependencies. On this computer, its interpreter is `C:\Users\Lenovo\.venvs\resq-handtracking\Scripts\python.exe`. Start the rescue scene and run:

```powershell
& "C:\Users\Lenovo\.venvs\resq-handtracking\Scripts\python.exe" webcam_hand_controller.py --preview
```

In build mode, pinch to place the current tool or drag an existing debris piece (press Z to undo a drag), move an open palm to pan the camera, and point to place a hazard marker. The sender uses localhost UDP port 5005. Mouse and keyboard controls continue to work when the webcam is off.

![screencap](/screencap/screencap1_360p.gif)

Hide and seek is an asymmetric cooperative-competitive environment for [Unity ML-Agents](https://github.com/Unity-Technologies/ml-agents) framework. The agents are divided into two teams: hiders and seekers. The goal of seekers is to maintain a line of sight on hiders, while the goal of hiders is to remain hidden throughout the game. The environment supports the predator-prey gameplay variant, where the goal of seekers is to capture hiders instead of simply having a line of sight on them. This repository was used for training and evaluation in the upcoming paper FCSP: Fictitious Co-Self-Play for Team-based Multi-agent Reinforcement Learning (preprint available on request).

## Requirements
The project was made using Unity 2021.3.10f1 with ML-Agents Release 20 (Unity package version 2.3.0-exp.2). To train the agents, [the ML-Agents Python package](https://github.com/Unity-Technologies/ml-agents/blob/release_20_docs/docs/Installation.md#install-the-mlagents-python-package) is required.

## Inference
To run inference, use the `Scenes/Test` scene. Checkpoints for hiders and seekers can be loaded in the Unity inspector in the `Game Controller` script properties of the platform object. After loading checkpoints, the easiest way to run inference is to run the scene in the editor. Alternatively, you can build the binary (go to `File > Build Settings`, add `Test` scene to build, set the desired target platform, then click Build). The binary can be run using the following command:
```
./unity-ml-agents_hide-and-seek.x86_64 [game_params=<path/to/game_params_config.json>] [arena_params=<path/to/arena_params_config.json>]
```
Note that checkpoints for inference cannot be loaded using game controller JSON configs.

## Training
To run training, use the `Scenes/Training` scene. You can adjust the number of training platforms by simply deleting or duplicating platform objects and stacking them vertically.
Training can be run using the following command:
```
mlagents-learn --env <path/to/hide-and-seek_binary> --num-envs 3 --no-graphics <path/to/ml-agents_config.yaml> --run-id <run_id> [--env-args <game_params=<path/to/game_params_config.json> arena_params=<path/to/arena_params_config.json>]
```
See the [ML-Agents getting started guide](https://github.com/Unity-Technologies/ml-agents/blob/release_20_docs/docs/Getting-Started.md) for more details on using the ML-Agents trainer script.

## Testing 
For testing the trained checkpoints, we provide a separate testing framework in separate repository [available here](https://github.com/MIMUW-RL/marl_team_based_testers) with a short documentation available.

## Environment Settings
The following tables describe the parameters of `Game Controller` and `Map Generator` scripts attached to platform objects.
The parameters can be either changed in the Unity inspector or overridden by passing a JSON config to the environment binary. Example configs are located in the `config` directory. The config files for hide and seek gamemode can be found in `config/default` and the config files for predator-prey gamemode can be found in `config/predator_prey`. The `config/borderless` shows an example of an arena that is not restricted by walls and penalizes agents for trying to go out of bounds instead.

### Game Parameters
| **Parameter** | **Description** |
|---------------|-----------------|
| `episodeSteps` | (default = `500`) The length of a single episode in environment steps. |
| `gracePeriodFraction` | (default = `0.4`) The fraction of episode steps during which hider actions are locked. E.g., for `episodeSteps = 500` and `gracePeriodFraction = 0.4`, the first 200 steps of each episode are considered warmup. |
| `coneAngle` | (default = `67.5`) The fov of agents used for calculating rewards. |
| `rewards` | A list of individual rewards to be awarded to agents. Every list entry should be a dictionary with two keys: `type` and `weight`. For a description of all reward types, refer to the table below. |
| `winCondition` | What event is considered a win for either team. For a description of all win condition types, refer to the table below. |
| `winConditionRewardMultiplier` | The group reward given to the team that satisfied the set winning condition at the end of every episode. |
| `arenaSize` | (default = `25`) The size of the playable area of the platform used for giving out of bounds penalty to agents. |
| `allowCapture` | Whether a hider being too close to a seeker should result in him getting captured and removed from the game. |
| `captureDistance` | (default = `1.2`) The distance between the hider and seeker at which the hider is considered captured. Has effect only if `allowCapture` is enabled. |
| `seekersCaptureGoal` | (default = `3`) The number of hiders that seekers need to capture to win. Has effect only if `allowCapture` is enabled and `winCondition` is set to `2`. |
| `useCoplay` | (default = `false`) Whether to use coplay during training. |
| `numberOfCoplayAgents` | (default = `1`) The number of coplay agents per episode. |
| `selfPlayRatio` | (default = `0.5`) The probability of using coplay each episode. |

### Arena Parameters
| **Parameter** | **Description** |
|---------------|-----------------|
| `mapSize` | (default = `25.0`) The size of the playable area of the platform used during map generation. Should be equal to `arenaSize` set in game parameters. |
| `wallsPosition` | (default = `0.0`) The distance of walls from the center of the platform. If set to `0`, the distance will match `arenaSize`. |
| `numHidersMin` | (default = `3`) The minimum number of hiders in team. |
| `numHidersMax` | (default = `3`) The maximum number of hiders in team. Should be at most `4` and not smaller than `numHidersMin`. |
| `numSeekersMin` | (default = `3`) The minimum number of seekers in team. |
| `numSeekersMax` | (default = `3`) The maximum number of seekers in team. Should be at most `4` and not smaller than `numSeekersMin`. |
| `agentRadius` | (default = `0.75`) The radius around each agent at which no objects will spawn during map generation. |
| `instantiateBoxes` | (default = `true`) Whether boxes should be spawned every episode. |
| `numBoxesMin` | (default = `5`) The minimum number of boxes which spawn every episode. |
| `numBoxesMax` | (default = `5`) The maximum number of boxes which spawn every episode. Should be not smaller than `numBoxesMin`. |
| `objectRadius` | (default = `1.5`) The radius around each box at which no other objects will spawn during map generation. |

### Reward Types
| **ID** | **Editor name** | **Description** |
|--------|-----------------|-----------------|
| `0` | Visibility Individual | `-weight` or `+weight` given every step, depending on the individual visibility criteria. A hider gets a positive reward iff no seeker has a line of sight on him. A seeker gets a positive reward iff he has a line of sight on at least one hider. |
| `1` | Visibility Team | `-weight` or `+weight` given every step, depending on the team visibility criteria. Hiders get a positive reward iff no seeker has a line of sight on any hider. Seekers get a positive reward iff at least one seeker has a line of sight on at least one hider. |
| `2` | Capture | Every time a seeker captures a hider, the caught hider is given a `-weight` penalty and seeker who captured him gets a `+weight` reward. Has effect only if `allowCapture` is enabled. |
| `3` | Oob Penalty | `-weight` penalty given every step to every agent who is outside of the box of size `arenaSize` around the center of the arena. |

### Win Condition Types
| **ID** | **Editor name** | **Description** |
|--------|-----------------|-----------------|
| `0` | None | No team is considered to be winning, and no group rewards are given. |
| `1` | Line Of Sight | Seekers win if, at any step after the warmup, at least one seeker has a line of sight on at least one hider. Hiders win otherwise. |
| `2` | Capture | Seekers win if they capture at least `seekersCaptureGoal` hiders during the episode. Hiders win otherwise. |


