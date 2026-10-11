from reader import UnityReader

def handle(sensors):
    print(sensors)

UnityReader(port=5000, on_message=handle).run()