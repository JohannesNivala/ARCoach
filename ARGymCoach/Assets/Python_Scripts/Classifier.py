import sys
import os
import matplotlib.pyplot as plt
import numpy as np
from skimage.measure import label, regionprops
from scipy.ndimage import center_of_mass, shift
from sklearn import svm
from skimage.transform import resize
from sklearn.preprocessing import StandardScaler
from sklearn.pipeline import make_pipeline
from skimage.metrics import structural_similarity as ssim
from PIL import Image
import io
import pickle
import base64
import time



def segment(im, red_thresh=0.90, rel_factor=2.0):
    imf = im.astype(np.float32)
    if imf.max() > 1.0:
        imf = imf / 255.0

    r = imf[:, :, 0]
    g = imf[:, :, 1]
    b = imf[:, :, 2]

    # red is sufficiently bright and significantly stronger than G and B
    mask = (r > red_thresh) & (r > g * rel_factor) & (r > b * rel_factor)
    return mask


def segment2feature(Si):
    ys, xs = np.nonzero(Si)   
    y_min, y_max, x_min, x_max = ys.min(), ys.max(), xs.min(), xs.max()
    content = Si[y_min:y_max+1, x_min:x_max+1]
    h, w = content.shape
    # We determine the padding needed to make it a square image
    max_dim = max(h, w)
    # Calculate padding 
    pad_h = max_dim - h
    pad_w = max_dim - w
    pad_top = pad_h // 2
    pad_bot = pad_h - pad_top
    pad_left = pad_w // 2
    pad_right = pad_w - pad_left
    # Pad the content to make it a square
    padded_content = np.pad(content, ((pad_top, pad_bot), (pad_left, pad_right)))
    # Resize the square image and center
    target_size = (600, 600)
    Si_resized = resize(padded_content, target_size, order=0)
    cy, cx = center_of_mass(Si_resized)
    sy, sx = np.array(Si_resized.shape) // 2
    shift_y, shift_x = sy - cy, sx - cx
    Si_centered = shift(Si_resized, shift=(shift_y, shift_x), order=0)
    # Gives properties used below for features
    props = regionprops(label(Si_centered))

    centered_nomalized = Si_centered/Si_centered.size
    #props = regionprops(label(centered_nomalized))
    h, w = centered_nomalized.shape
    # Find convex hull and takes the ratio of pixels in the region to pixels of the convex hull image.
    solidity = props[0].solidity

    area = props[0].area
    perimeter = props[0].perimeter

    grid_size = 8
    cell_h, cell_w = h // grid_size, w // grid_size
    grid_densities = []
    
    # Density for the grids
    for i in range(grid_size):
        for j in range(grid_size):
            cell = centered_nomalized[i*cell_h:(i+1)*cell_h, j*cell_w:(j+1)*cell_w]
            grid_densities.append(cell.sum())


    features = np.array([
        solidity,
        *grid_densities,        
    ])
    return features.reshape(-1,1)


def train_classifier(X_features, Y):
    # X_features is a list of feature vectors (1D arrays)
    classdata = make_pipeline(StandardScaler(), svm.SVC(gamma='auto', probability=True))
    classdata.fit(X_features, Y)
    return classdata


def load_and_process_data(good_dir, bad_dir):
    """Loads images, processes them, and prepares features and labels."""
    X_features = []
    Y_labels = []
    image_extensions = ('.png', '.jpg', '.jpeg')

    # Class 0: Bad Pictures
    print(f"Processing images from '{bad_dir}' (Class 0 - Bad)...")
    for filename in os.listdir(bad_dir):
        if filename.lower().endswith(image_extensions):
            filepath = os.path.join(bad_dir, filename)
            try:
                # Use plt.imread to match your processing pipeline
                img = plt.imread(filepath)
                if img.ndim == 3:
                    img = img[..., 0]                 
                # 2. Extract features
                features = segment2feature(img)
                
                X_features.append(features.flatten())
                Y_labels.append(0) # Assign label 0 for "Bad"
            except Exception as e:
                print(f"Skipping {filename} in {bad_dir}. Error: {e}")

    # Class 1: Good Pictures
    print(f"Processing images from '{good_dir}' (Class 1 - Good)...")
    for filename in os.listdir(good_dir):
        if filename.lower().endswith(image_extensions):
            filepath = os.path.join(good_dir, filename)
            try:
                img = plt.imread(filepath)
                if img.ndim == 3:
                    img = img[..., 0]   
                features = segment2feature(img)
                
                X_features.append(features.flatten())
                Y_labels.append(1) # Assign label 1 for "Good"
            except Exception as e:
                print(f"Skipping {filename} in {good_dir}. Error: {e}")
                
    return np.array(X_features), np.array(Y_labels)

def save_classifier(classifier, filename="classifier.pkl"):
    """Saves the trained classifier object to a file using pickle."""
    with open(filename, 'wb') as file:
        pickle.dump(classifier, file)
    print(f"\nClassifier successfully saved to {filename}")

def load_classifier(filename="classifier.pkl"):
    """Loads the trained classifier object from a file."""
    with open(filename, 'rb') as file:
        classifier = pickle.load(file)
    print(f"Classifier successfully loaded from {filename}")
    return classifier

def classify_single_image(img, trained_classifier):
    """
    Loads a single image, processes it through the feature pipeline, 
    and predicts its class using the trained classifier.
    
    Returns: 0 for 'Bad', 1 for 'Good' (based on your training labels).
    """
    try:
        seg_img = segment(img)

        features = segment2feature(seg_img)
        
        input_feature_vector = features.flatten().reshape(1, -1)
        
        prediction = trained_classifier.predict(input_feature_vector)
        
        return int(prediction[0])
        
    except Exception as e:
        print(f"Error processing and classifying image: {e}")
        return None
    
def setupClassifier():
# --- Configuration ---
    GOOD_IMAGES_FOLDER = 'good_proccessed' # Folder with images for class 1
    BAD_IMAGES_FOLDER = 'bad_proccessed'   # Folder with images for class 0
    CLASSIFIER_FILENAME = 'quality_classifier.pkl'
    # Ensure you have 'good_images' and 'bad_images' folders set up with your files!
    
    ## 1. Load Data and Extract Features
    X, Y = load_and_process_data(GOOD_IMAGES_FOLDER, BAD_IMAGES_FOLDER)
    
    if len(X) == 0:
        print("\n🚨 ERROR: No features extracted. Check your folder paths and image files.")
    else:
        print(f"\nTotal images processed: {len(X)}")
        print(f"Feature vector size: {X.shape[1]}")
        
        ## 2. Train the Classifier
        print("\nStarting classifier training...")
        # Note: train_classifier expects a list of 1D feature arrays, which X is.
        classdata = train_classifier(X, Y)
        print("Training complete.")
        
        ## 3. Save the Classifier
        save_classifier(classdata, CLASSIFIER_FILENAME)

""" if __name__ == "__main__": 
    # --- Example of Loading and Testing ---
    CLASSIFIER_FILENAME = 'quality_classifier.pkl'
    NEW_IMAGE_PATH = 'test.png'
    # 1. Load the trained classifier
    try:
        loaded_classifier = load_classifier(CLASSIFIER_FILENAME)
    except FileNotFoundError:
        print(f"🚨 Error: Classifier file '{CLASSIFIER_FILENAME}' not found. Please train the classifier first.")
        exit()
        
    # 2. Classify the new image
    start = time.time()
    predicted_class = classify_single_image(NEW_IMAGE_PATH, loaded_classifier)
    end = time.time()
    print(end-start)


    # 3. Print the result
    if predicted_class is not None:
        result_label = "Good Picture (Class 1)" if predicted_class == 1 else "Bad Picture (Class 0)"
        print(f"\nThe image '{NEW_IMAGE_PATH}' is classified as: **{result_label}**") """

def classify_and_print_result(image_path, base64_data):
    png_bytes = base64.b64decode(base64_data)
    CLASSIFIER_FILENAME = 'quality_classifier.pkl'
    image = np.array(Image.open(io.BytesIO(png_bytes)))

    # 2. Load the trained classifier
    try:
        loaded_classifier = load_classifier(CLASSIFIER_FILENAME)
    except FileNotFoundError:
        print("-2") # Error code for missing classifier
        sys.exit(1)
        
    # 3. Classify the new image
    predicted_class = classify_single_image(image, loaded_classifier)

    # 4. Return the predicted class to Unity via standard output
    if predicted_class is not None:
        # Unity reads the FIRST line printed to standard output
        # Print only the integer result (0 or 1)
        print(predicted_class) 
        
        # Optional: Log the result to standard error (Unity usually ignores this)
        # sys.stderr.write(f"Classified {image_path} as {predicted_class}\n")
    else:
        # Error during classification (e.g., failed image read/segmentation)
        print("-3") 
        sys.exit(1)

if __name__ == "__main__": 
    if len(sys.argv) > 1:
        classify_and_print_result(sys.argv[1]) 
    else:
        print("-1")