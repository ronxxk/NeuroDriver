import socket
import json


class UnityReader:
    """TCP server that receives newline-delimited JSON from Unity."""

    def __init__(self, host="127.0.0.1", port=5000, on_message=None):
        self.host = host
        self.port = port
        self.on_message = on_message or self.default_handler
        self.server = None
        self.connection = None
        self.address = None
        self.buffer = ""

    # --- lifecycle ---------------------------------------------------------
    def start(self):
        self.server = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
        self.server.setsockopt(socket.SOL_SOCKET, socket.SO_REUSEADDR, 1)
        self.server.bind((self.host, self.port))
        self.server.listen(1)
        print(f"Waiting for Unity on {self.host}:{self.port}...")
        self.connection, self.address = self.server.accept()
        print("Unity connected!")

    def stop(self):
        for sock in (self.connection, self.server):
            if sock:
                try:
                    sock.close()
                except OSError:
                    pass
        self.connection = self.server = None

    # --- receiving ---------------------------------------------------------
    def read_messages(self):
        """Blocking read. Returns a list of parsed messages, or None if Unity disconnected."""
        try:
            data = self.connection.recv(4096)
        except (ConnectionResetError, ConnectionAbortedError, OSError):
            return None
        if not data:
            return None

        self.buffer += data.decode("utf-8")
        messages = []

        while "\n" in self.buffer:
            line, self.buffer = self.buffer.split("\n", 1)
            if not line.strip():
                continue
            try:
                messages.append(json.loads(line))
            except json.JSONDecodeError:
                print("Invalid data:", line)

        return messages

    def send(self, obj):
        try:
            self.connection.sendall((json.dumps(obj) + "\n").encode("utf-8"))
        except (ConnectionResetError, ConnectionAbortedError, BrokenPipeError, OSError):
            pass  # Unity went away; read_messages will notice on the next loop

    def run(self):
        """Keeps the server up so you can press Play in Unity repeatedly."""
        try:
            self.server = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
            self.server.setsockopt(socket.SOL_SOCKET, socket.SO_REUSEADDR, 1)
            self.server.bind((self.host, self.port))
            self.server.listen(1)
            while True:
                print(f"Waiting for Unity on {self.host}:{self.port}...")
                self.connection, self.address = self.server.accept()
                self.buffer = ""
                print("Unity connected!")
                while True:
                    messages = self.read_messages()
                    if messages is None:
                        print("Unity disconnected.")
                        break
                    for msg in messages[-1:]:
                        self.on_message(msg)
                self.connection.close()
        except KeyboardInterrupt:
            pass
        finally:
            self.stop()

    # --- default handler ---------------------------------------------------
    @staticmethod
    def default_handler(sensors):
        print("Sensors:", sensors)


def on_message(cars):
    controls = {}
    print(f"{len(cars)} cars:", list(cars.keys()))

    for car_id, d in cars.items():
        sensors = d["sensors"]
        crashed = d["crashed"]
        controls[car_id] = {"steer": 0.0, "throttle": 1.0}
    #reader.send(controls)


if __name__ == "__main__":
    reader = UnityReader(on_message=on_message)
    reader.run()