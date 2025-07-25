#!/usr/bin/env python3
import sys, io, time, struct
import numpy as np
from PIL import Image
import torch
from torch import nn
import numpy as np
import cv2
from PIL import Image
from torchvision import models, transforms
import time

class FrameHumanAnalysis:
    def __init__(self, frame: Image.Image, model : nn.Module, outline_color = (0, 0, 255), sub_outline_color = (0, 255, 0)):
        self.__frame = frame
        self.__frame_width, self.frame_height = frame.size
        self.__orig_cv = cv2.cvtColor(np.array(frame), cv2.COLOR_RGB2BGR)
        self.__model = model
        self.__outline_color = outline_color
        self.__sub_outline_color = sub_outline_color
        self.__layer_count = 3
        
    def get_gradient_external(self, include_outside = False) -> np.ndarray:
        """
        External method to get the gradient of the distance function.
        This is a wrapper around the internal method to allow for external access.
        """
        human_mask = self.get_human()
        human_outline = self.get_human_outline(human_mask)
        distance_function = self.get_distance_function(human_outline)
        grad = self.get_gradient(distance_function)    
        if(include_outside == False):
            grad[human_mask == 0] = 0
        return grad
        
    def analyze_frame(self, layer_count: int = 3) -> np.ndarray:
        human_mask = self.get_human()
        human_outline = self.get_human_outline(human_mask)
        
        
        cv2.imwrite("human_mask.png", human_outline * 255)
        
        distance_function = self.get_distance_function(human_outline)
        gradient = self.get_gradient(distance_function)
        
        sub_level_outline = self.get_sub_level_set_outline(
            human_outline,
            distance_function,
            layer_count,
            human_mask,
            include_outside=False
        )
        
        return sub_level_outline
        
        # human_mask_img = cv2.cvtColor(human_mask * 255, cv2.COLOR_GRAY2BGR)
        # human_outline_img = cv2.cvtColor(human_outline * 255, cv2.COLOR_GRAY2BGR)
        sub_level_set_img = self.getimg_sub_level_sets(distance_function, human_mask, human_outline, layer_count, True)
        
        arrows_img = self.getimg_gradient_arrows(
            gradient,
            base_image=sub_level_set_img,
            step=15,
            scale=0.1,            # tweak so arrows are visible
            color=(255, 255, 255),
            thickness=1,
            tip_length=0.2
        )
        
        # Combine images
        # Create a 2x2 grid image with human_mask_img, human_outline_img, sub_level_set_img, and arrows_img
        
                
    def get_human(self) -> np.ndarray:
        preprocess = transforms.Compose([
            transforms.ToTensor(),
            transforms.Normalize(mean=[0.485, 0.456, 0.406], std=[0.229, 0.224, 0.225])
        ])
        input_tensor = preprocess(self.__frame).unsqueeze(0) # type: ignore
        
        with torch.no_grad():
            output = self.__model(input_tensor)['out'][0]      # (21, H, W)
            pred = output.argmax(0).cpu().numpy()       # (H, W)
        
        return (pred == 15).astype(np.uint8)

    def get_human_outline(self, human_mask: np.ndarray, kernel_size: int = 3) -> np.ndarray:
        kernel = np.ones((kernel_size, kernel_size), dtype=np.uint8)
        eroded = cv2.erode(human_mask, kernel, iterations=1)
        outline = human_mask - eroded
        return outline
    
    def get_distance_function(self, outline_mask: np.ndarray) -> np.ndarray:
        inv_outline = (1 - outline_mask).astype(np.uint8)
        dist_map = cv2.distanceTransform(inv_outline, distanceType=cv2.DIST_L2, maskSize = cv2.DIST_MASK_PRECISE)
        return dist_map
    
    def get_sub_level_set_outline(self, human_outline: np.ndarray, distance_function: np.ndarray, layer_count: int, human_mask: np.ndarray, include_outside = False) -> np.ndarray:
        self.__layer_count = layer_count
        
        normed_level_set = cv2.normalize(distance_function, None, 0, 255, cv2.NORM_MINMAX).astype(np.uint8)  # type: ignore

        layer_length = 255 // layer_count
        sub_level_sets_outline_mask = np.zeros_like(normed_level_set, dtype=np.uint8)
        sub_level_sets_outline_mask[normed_level_set % layer_length == 0] = 1
        
        if(include_outside == False):
            sub_level_sets_outline_mask[human_mask == 0] = 0
        
        # Generate a black and white image from the mask
        sub_level_sets_outline = (sub_level_sets_outline_mask * 255).astype(np.uint8)
        
        return sub_level_sets_outline_mask
    
    def getimg_sub_level_sets(self, distance_function: np.ndarray, human_mask: np.ndarray, outline, layer_count: int, include_outside = False) -> np.ndarray:
        normed_level_set = cv2.normalize(distance_function, None, 0, layer_count, cv2.NORM_MINMAX).astype(np.uint8)  # type: ignore
        normed_level_set = cv2.normalize(normed_level_set, None, 0, 255, cv2.NORM_MINMAX).astype(np.uint8)  # type: ignore
        
        img = np.zeros_like(self.__orig_cv)
        img[..., 1] = normed_level_set  # Set the green channel to normed_level_set values
        
        if include_outside == False:
            img[human_mask == 0] = 0        # Set background to black
        img[outline == 1] = self.__outline_color
        
        return img
    
    def get_gradient(self, distance_function: np.ndarray) -> np.ndarray:
        gradient_x = cv2.Sobel(distance_function, cv2.CV_64F, 1, 0, ksize=5)
        gradient_y = cv2.Sobel(distance_function, cv2.CV_64F, 0, 1, ksize=5)
        gradient = np.stack((gradient_x, gradient_y), axis=-1)
        
        return gradient
    
    def getimg_gradient_arrows(self,
                            gradient: np.ndarray,
                            base_image: np.ndarray,
                            step: int = 10,
                            scale: float = 1.0,
                            color: tuple = (0, 255, 0),
                            thickness: int = 1,
                            tip_length: float = 0.3) -> np.ndarray:
        """
        Draw arrows indicating the gradient at regular grid points.

        Args:
            gradient: np.ndarray of shape (H, W, 2), gradient[...,0]=dx, gradient[...,1]=dy
            base_image: BGR image to draw arrows onto (will be copied)
            step: number of pixels between arrows in both x and y
            scale: factor to multiply the raw gradient vector for visibility
            color: BGR tuple for arrow color
            thickness: arrow line thickness
            tip_length: relative length of arrow tip

        Returns:
            Annotated image (copy of base_image).
        """
        h, w = gradient.shape[:2]
        out = np.zeros_like(base_image)

        for y in range(0, h, step):
            for x in range(0, w, step):
                gx, gy = gradient[y, x]
                start_pt = (x, y)
                end_pt = (
                    int(x + scale * gx),
                    int(y + scale * gy)
                )
                cv2.arrowedLine(out, start_pt, end_pt, color,
                                thickness=thickness, tipLength=tip_length)
        return out

def main():
    model = models.segmentation.deeplabv3_mobilenet_v3_large(pretrained=True).eval()
    # print("Sleeping for 15 seconds to allow camera to initialize...")
    # time.sleep(5)
    # print("15 seconds passed, starting video stream...")
    
    # Open default camera (usually index 0)
    cap = cv2.VideoCapture(0)
    
    cnt = 0

    if not cap.isOpened():
        print("Cannot open camera")
        exit()

    while True:
        # Capture frame-by-frame
        ret, frame = cap.read()   

        # If frame is read correctly ret is True
        if not ret:
            print("Can't receive frame (stream end?). Exiting ...")
            break

            # Convert the frame (BGR from OpenCV) to RGB and then to PIL Image
        frame_rgb = cv2.cvtColor(frame, cv2.COLOR_BGR2RGB)
        pil_img = Image.fromarray(frame_rgb)
        
        image_analysis = FrameHumanAnalysis(pil_img, model, (0, 0, 255), (0, 255, 0))
        
        img = image_analysis.analyze_frame(20)
        
        # Encode the image as PNG and get the byte array
        ys, xs = np.nonzero(img)                     # arrays of row/col indices
        n = len(xs)

        # pack: 4‑byte count, then n pairs of 2‑byte unsigned shorts (=> max dim 65k)
        buf = struct.pack('<I', n)
        for x, y in zip(xs, ys):
            buf += struct.pack('<HH', x, y)

        # write to stdout
        sys.stdout.buffer.write(buf)
        sys.stdout.buffer.flush()
        
        cnt += 1        
        # cv2.imshow("Me", img)
        # # Press 'q' to quit
        # if cv2.waitKey(1) == ord('q'):
        #     break 
        # if cnt % 5 != 0:
        #     continue
        
        # print("img shape:", img.shape)
        

if __name__ == "__main__":
    main()
    
    
#farnebeck's method