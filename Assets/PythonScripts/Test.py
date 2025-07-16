#!/usr/bin/env python3
import sys, io, time, struct
import numpy as np
from PIL import Image

# Target frame rate
fps = 30
interval = 1.0 / fps

def generate_frame():
    # Replace this with your real mask generation.
    # Must be H×W×3, dtype=uint8.
    h, w = 512, 512
    arr = np.zeros((h, w, 3), dtype=np.uint8)
    arr[:, :] = 255  # white background
    arr[64:192, 64:192] = 0  # black square in the center
    return arr

while True:
    arr = generate_frame()
    img = Image.fromarray(arr)

    # Encode to PNG in memory
    buf = io.BytesIO()
    img.save(buf, format="PNG")
    data = buf.getvalue()

    # Write 4‑byte big-endian length prefix, then PNG bytes
    sys.stdout.buffer.write(struct.pack(">I", len(data)))
    sys.stdout.buffer.write(data)
    sys.stdout.buffer.flush()

    time.sleep(1000000)
