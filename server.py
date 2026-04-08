import asyncio
import websockets
import classify
import numpy
from PIL import Image
import io
import base64

async def handler(websocket):
    async for message in websocket:
        if message == "Unity has connected!" or message == "Unity has disconnected!":
            print(message)
        else:

            print(f"Received image from Unity. Classifying...")
            message_bytes = base64.b64decode(message)
            image = numpy.array(Image.open(io.BytesIO(message_bytes)))
            result = classify.classify_single_image(image)
            print(f"Classification complete!")

            await websocket.send(str(result))
            print(f"Sent back result to Unity.")

async def main():
    async with websockets.serve(handler, "0.0.0.0", 8765, max_size=None):
        print("Python WebSocket Server started on ws://localhost:8765")
        await asyncio.Future()

if __name__ == "__main__":
    asyncio.run(main())