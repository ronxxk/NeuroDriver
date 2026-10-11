import os
import random
import numpy
from reader import UnityReader

RESET = False    # True = ignore the saved brain and start from scratch
ELITE = 4           # how many top brains breed the next generation
MUT_RATE = 0.10      # chance each weight gets nudged
BEST_FILE = os.path.join(os.path.dirname(os.path.abspath(__file__)), "best_brain.npz")
SIZES = [9, 9, 4, 2]   # 7 rays + 2 speeds -> 9 -> 4 -> (throttle, steer)
TIME_PENALTY = 0.03   # score lost per second alive; raise it to favor speed harder
TICK_RATE = 50        # messages per second = 1 / Time.fixedDeltaTime (0.02 -> 50)


class Brain:
    def __init__(self, params=None):
        if params is None:
            params = []
            for a, b in zip(SIZES, SIZES[1:]):
                params.append(numpy.random.randn(a, b) * numpy.sqrt(2.0 / a))  # weights
                params.append(0.1 * numpy.random.randn(1, b))                  # biases
        self.params = params

    def copy(self):
        return Brain([p.copy() for p in self.params])

    def mutated(self, rate, std):
        new = []
        for p in self.params:
            mask = numpy.random.rand(*p.shape) < rate
            new.append(p + mask * numpy.random.randn(*p.shape) * std)
        return Brain(new)

    def think(self, sensors):
        x = numpy.array(sensors, dtype=float).reshape(1, -1)
        n_layers = len(self.params) // 2
        for i in range(n_layers):
            x = x @ self.params[2 * i] + self.params[2 * i + 1]
            x = numpy.tanh(x) if i == n_layers - 1 else numpy.maximum(0, x)
        throttle, steer = x[0]
        return float(throttle), float(steer)


best_saved = -1.0


def save_brain(brain, fitness):
    numpy.savez(BEST_FILE, fitness=fitness, *brain.params)


def load_brain():
    global best_saved
    if RESET or not os.path.exists(BEST_FILE):
        return None
    try:
        data = numpy.load(BEST_FILE)
        params = [data[f"arr_{i}"] for i in range(len(data.files) - 1)]
        brain = Brain(params)
        brain.think([0.0] * SIZES[0])   # fails here if the architecture changed
        best_saved = -1e9
        print(f"Loaded saved brain (fitness {best_saved:.2f})")
        return brain
    except Exception as e:
        print("Could not load saved brain, starting random:", e)
        return None


brains = {}
best_fit = {}        # best fitness each car reached in the current generation
current_gen = None
tick = 0
stale = 0   # generations without a new record
alive_ticks = {}


def next_population(elites, n, boost=1.0):
    n_random = 0 if n < 5 else (2 if n < 12 else 4)
    pop = [elites[0].copy()]
    for _ in range(n - 1 - n_random):
        parent = random.choice(elites)
        rate = min(0.6, MUT_RATE * boost)
        pop.append(parent.mutated(rate, random.uniform(0.05, 0.4) * boost))
    for _ in range(n_random):
        pop.append(Brain())
    return pop

def init_population(ids):
    saved = load_brain()
    if saved is None:
        for cid in ids:
            brains[cid] = Brain()
    else:
        for cid, b in zip(ids, next_population([saved], len(ids))):
            brains[cid] = b


def score(i):
    return best_fit.get(i, 0.0) - TIME_PENALTY * alive_ticks.get(i, 0) / TICK_RATE


def evolve():
    global best_saved, stale
    ids = sorted(brains)
    ranked = sorted(ids, key=score, reverse=True)
    top = score(ranked[0])
    avg = sum(score(i) for i in ids) / len(ids)

    if top > best_saved + 0.05:
        stale = 0
    else:
        stale += 1

    if top > best_saved:
        best_saved = top
        save_brain(brains[ranked[0]], top)
        print(f"  new record, saved ({top:.2f})")

    print(f"gen {current_gen} done | best {top:.2f} | avg {avg:.2f} | record {best_saved:.2f} | stale {stale}")

    boost = 1 + 0.25 * min(stale, 12)
    elites = [brains[i] for i in ranked[:ELITE]]
    for cid, b in zip(ids, next_population(elites, len(ids), boost)):
        brains[cid] = b
                
def handle(msg):
    global current_gen, tick
    tick += 1

    gen = msg["gen"]
    cars = msg["cars"]

    if not brains:
        init_population(sorted(cars))

    if current_gen is None:
        current_gen = gen
    elif gen != current_gen:
        evolve()
        best_fit.clear()
        alive_ticks.clear()
        current_gen = gen

    controls = []
    for car_id, d in cars.items():
        if car_id not in brains:
            brains[car_id] = Brain()

        best_fit[car_id] = max(best_fit.get(car_id, 0.0), d["fitness"])
        if not d["crashed"]:
            alive_ticks[car_id] = alive_ticks.get(car_id, 0) + 1
        if d["crashed"]:
            throttle, steer = 0.0, 0.0
        else:
            throttle, steer = brains[car_id].think(d["sensors"])

        controls.append({"id": car_id, "steer": steer, "throttle": throttle})

    unity.send({"controls": controls})


unity = UnityReader(on_message=handle)
unity.run()