# AI Race Car: neuroevolution in Unity + Python

2D drift cars learn to drive around a track using an evolutionary neural network, with no hand-written driving rules and no training data. Unity runs the simulation (physics, track, cars). A separate Python program holds the neural networks and decides what each car does. The two talk over a local TCP socket.

## Demo

### Learning over generations

In generation 1 the cars have random brains and crash almost immediately. Each generation, the best brains are kept and mutated, and by about generation 9 the cars complete a full lap.

![Training timelapse](media/GenerationTimeLapse.gif)

### Generalization test: mirrored track

The same brain, trained only on the original layout, on a mirrored track it has never seen. Only one car finished, which shows the brain partly memorized the original track instead of learning general driving.

![Mirrored track test](media/MirroredTrack.gif)

## How it works

Every physics tick, each car casts seven rays in a fan (-60° to +60°) to measure the distance to the walls, and also reports its forward and sideways speed. Unity sends these nine numbers for all cars as one JSON message. Python feeds each car's numbers through its own small neural network (9 inputs, hidden layers of 9 and 4 ReLU neurons, 2 tanh outputs) and replies with a throttle and a steering value, which Unity applies to the car's drift controller.

## How it learns

- **Fitness:** cars are scored with ordered checkpoints around the track. Fitness is the number of checkpoints passed in order, plus fractional progress toward the next one. A small time penalty means faster cars win.
- **Death:** cars that crash, or make no progress for several seconds, are frozen in place.
- **Generations:** when every car is dead or a time limit hits, Unity resets all cars to the start and increments the generation counter.
- **Evolution:** Python ranks the brains and keeps the top few. The next generation is one unchanged copy of the best brain, mutated copies of the top performers (Gaussian noise on a fraction of the weights), and a few fully random newcomers. If progress stalls, mutation strength increases automatically.
- **Saving:** the best brain is saved to `best_brain.npz` and loaded on the next run, so training can continue.

## Results

- From random weights, the cars complete a full lap in about nine generations.
- A 90° rotated copy of the track works, which is expected, since the sensors are relative to the car.
- A mirrored track was completed by only one car, so the brain generalizes only partly.

## Run it

**Requirements:** Unity 6 or newer with the Input System package, Python 3 and NumPy (`pip install numpy`).

1. Start the Python side first. It acts as the server and waits for Unity:
   ```
   python Assets/scripts/Network/network.py
   ```
2. Open the project in Unity, open the scene, and press **Play**. The console prints `Connected to Python!`.
3. Watch the generation counter in the top-left corner. **P** pauses and resumes, and **H** hides the overlay for clean screenshots and recordings.

**Reset training:** delete `Assets/scripts/Network/best_brain.npz`, or set `RESET = True` at the top of `network.py`. Back up the file first, since the first generation of any new run overwrites it.

## Project structure

```
Car_neural_network/
├── Assets/
│   ├── Scenes/
│   └── scripts/
│       ├── Network/            Python side
│       │   ├── network.py      brains, evolution, save/load (run this)
│       │   ├── reader.py       TCP server, JSON message handling
│       │   └── best_brain.npz  best weights found so far
│       ├── CarSensors.cs       rays, speed, checkpoints, fitness, reset
│       ├── Carcontroller2d.cs  drift car physics
│       ├── SensorSender.cs     TCP client, sends sensors, applies controls
│       ├── GenerationManager.cs  generation reset, HUD, pause
│       ├── Checkpoint.cs       ordered checkpoint triggers
│       ├── CarSpawner.cs       spawns the car prefab N times
│       ├── RandomSpriteAssigner.cs  random look for each car
│       ├── CameraController.cs / CameraFollow2D.cs
│       └── SkidMarks2D.cs
└── media/                      demo GIFs
```

## Tech stack

- **Unity:** C#, 2D physics, raycasts
- **Python:** NumPy only, with a hand-written network and evolution (no ML framework)
- **Link:** TCP sockets with newline-delimited JSON

For the full design (protocol, fitness, evolution details, settings), see [architecture.md](architecture.md).

## Limitations and next steps

- Trained on a single track layout, so the brain partly memorizes it. Next: train across several tracks by alternating them between generations, then test on a layout it has never seen.
- Only 7 rays within ±60°, so sharp corners are seen late.
- Ideas: handbrake/drift as a third output, a lap timer, a training-curve plot, and racing against a human-driven car.
