import os
import matplotlib.pyplot as plt
import numpy as np
from skimage.measure import label, regionprops
from scipy.ndimage import center_of_mass, shift, sum, label
import scipy.ndimage
from sklearn import svm, tree
from skimage.transform import resize
from sklearn.preprocessing import StandardScaler
from sklearn.pipeline import make_pipeline
from sklearn.metrics import accuracy_score
import skimage.measure;
import pickle
import time

CLASSIFIER_FILENAME = 'v2_classifier.pkl'

def calculate_hit_rate(test_folder_root, trained_classifier):
    TEST_FOLDERS = {
        'lower_good_proccessed': 0,
        'middle_good_proccessed': 1,
        'higher_good_proccessed': 2,
        'left_bad_proccessed': 3,
        'right_bad_proccessed': 4,
    }
    image_extensions = ('.png', '.jpg', '.jpeg')
    y_true = []
    y_pred = []
    total_images = 0
    
    print(f"\n--- Starting Hit Rate Calculation on Test Data in '{test_folder_root}' ---")
    
    for folder_name, true_label in TEST_FOLDERS.items():
        folder_path = os.path.join(test_folder_root, folder_name)
        if not os.path.isdir(folder_path):
            print(f":warning: Warning: Test folder '{folder_path}' not found. Skipping.")
            continue
            
        print(f"Processing images in '{folder_name}' (True Label: {true_label})...")
        
        for filename in os.listdir(folder_path):
            if filename.lower().endswith(image_extensions):
                image_path = os.path.join(folder_path, filename)
                total_images += 1
                
                # Use your existing classification function
                img = plt.imread(image_path)
                if img.ndim == 3:
                    img = img[..., 0]

                try:
                    features = segment2feature(img)
                    
                    input_feature_vector = features.flatten().reshape(1, -1)
                    
                    prediction = loaded_classifier.predict(input_feature_vector)
                    
                    predicted_class = int(prediction[0])
                    
                except Exception as e:
                    print(f"Error processing and classifying image: {e}")
                
                if predicted_class is not None:
                    y_true.append(true_label)
                    y_pred.append(predicted_class)
                else:
                    pass 
    
    if total_images == 0:
        print(":rotating_light: Error: No images found in the test folders.")
        return 0.0

    print(f"\nTotal images successfully processed for testing: {len(y_true)} out of {total_images}")
    
    # Calculate the accuracy (hit rate)
    accuracy = accuracy_score(y_true, y_pred)
    
    return accuracy 

def segment(im, red_thresh=0.90, rel_factor=2.0, min_cluster_size=2000):
    imf = im.astype(np.float32)
    if imf.max() > 1.0:
        imf = imf / 255.0

    r = imf[:, :, 0]
    g = imf[:, :, 1]
    b = imf[:, :, 2]

    mask = (r > red_thresh) & (r > g * rel_factor) & (r > b * rel_factor)

    structure = np.array([[0,1,0],
                          [1,1,1],
                          [0,1,0]])

    labeled, num = scipy.ndimage.label(mask, structure=structure)
    sizes = sum(mask, labeled, range(1, num+1))

    large_clusters = np.zeros_like(mask)

    for i, size in enumerate(sizes):
        if size > min_cluster_size:
            large_clusters[labeled == (i+1)] = True

    return large_clusters


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
    props = regionprops(skimage.measure.label(Si_centered))

    centered_nomalized = Si_centered/Si_centered.size
    #props = regionprops(label(centered_nomalized))
    h, w = centered_nomalized.shape
    # Find convex hull and takes the ratio of pixels in the region to pixels of the convex hull image.
    solidity = props[0].solidity

    area = props[0].area
    perimeter = props[0].perimeter

    grid_size = 16
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
    #classdata = make_pipeline(StandardScaler(), svm.SVC(gamma='auto', probability=True))
    classdata = make_pipeline(StandardScaler(), tree.DecisionTreeClassifier(random_state = 42))
    classdata.fit(X_features, Y)
    return classdata


def load_and_process_data():
    LOWERGOOD_IMAGES_FOLDER = 'processed_data_folder/lower_good_proccessed'
    MIDDLEGOOD_IMAGES_FOLDER = 'processed_data_folder/middle_good_proccessed'
    HIGHERGOOD_IMAGES_FOLDER = 'processed_data_folder/higher_good_proccessed'
    LEFTBAD_IMAGES_FOLDER = 'processed_data_folder/left_bad_proccessed'
    RIGHTBAD_IMAGES_FOLDER = 'processed_data_folder/right_bad_proccessed'
    #WIDEBAD_IMAGES_FOLDER = 'processed_data_folder/wide_bad_proccessed'
    """Loads images, processes them, and prepares features and labels."""
    X_features = []
    Y_labels = []
    image_extensions = ('.png', '.jpg', '.jpeg')

    # Class 0: Lower Good Pictures
    print(f"Processing images from '{LOWERGOOD_IMAGES_FOLDER}' (Class 0 - Lower Good)...")
    for filename in os.listdir(LOWERGOOD_IMAGES_FOLDER):
        if filename.lower().endswith(image_extensions):
            filepath = os.path.join(LOWERGOOD_IMAGES_FOLDER, filename)
            try:
                img = plt.imread(filepath)
                if img.ndim == 3:
                    img = img[..., 0]                 
                features = segment2feature(img)
                
                X_features.append(features.flatten())
                Y_labels.append(0) # Assign label 0 for "´Lower Good"
            except Exception as e:
                print(f"Skipping {filename} in {LOWERGOOD_IMAGES_FOLDER}. Error: {e}")

    # Class 1: Middle Good Pictures
    print(f"Processing images from '{MIDDLEGOOD_IMAGES_FOLDER}' (Class 1 - Middle Good)...")
    for filename in os.listdir(MIDDLEGOOD_IMAGES_FOLDER):
        if filename.lower().endswith(image_extensions):
            filepath = os.path.join(MIDDLEGOOD_IMAGES_FOLDER, filename)
            try:
                img = plt.imread(filepath)
                if img.ndim == 3:
                    img = img[..., 0]                 
                features = segment2feature(img)
                
                X_features.append(features.flatten())
                Y_labels.append(1) # Assign label 1 for "Middle Good"
            except Exception as e:
                print(f"Skipping {filename} in {MIDDLEGOOD_IMAGES_FOLDER}. Error: {e}")

    # Class 2: Higher Good Pictures
    print(f"Processing images from '{HIGHERGOOD_IMAGES_FOLDER}' (Class 2 - Higher Good)...")
    for filename in os.listdir(HIGHERGOOD_IMAGES_FOLDER):
        if filename.lower().endswith(image_extensions):
            filepath = os.path.join(HIGHERGOOD_IMAGES_FOLDER, filename)
            try:
                img = plt.imread(filepath)
                if img.ndim == 3:
                    img = img[..., 0]                 
                features = segment2feature(img)
                
                X_features.append(features.flatten())
                Y_labels.append(2) # Assign label 2 for "Higher Good"
            except Exception as e:
                print(f"Skipping {filename} in {HIGHERGOOD_IMAGES_FOLDER}. Error: {e}")

    # Class 3: Left bad Pictures
    print(f"Processing images from '{LEFTBAD_IMAGES_FOLDER}' (Class 3 - Left Bad)...")
    for filename in os.listdir(LEFTBAD_IMAGES_FOLDER):
        if filename.lower().endswith(image_extensions):
            filepath = os.path.join(LEFTBAD_IMAGES_FOLDER, filename)
            try:
                img = plt.imread(filepath)
                if img.ndim == 3:
                    img = img[..., 0]   
                features = segment2feature(img)
                
                X_features.append(features.flatten())
                Y_labels.append(3) 
            except Exception as e:
                print(f"Skipping {filename} in {LEFTBAD_IMAGES_FOLDER}. Error: {e}")

    # Class 4: Right bad Pictures
    print(f"Processing images from '{RIGHTBAD_IMAGES_FOLDER}' (Class 4 - Right Bad)...")
    for filename in os.listdir(RIGHTBAD_IMAGES_FOLDER):
        if filename.lower().endswith(image_extensions):
            filepath = os.path.join(RIGHTBAD_IMAGES_FOLDER, filename)
            try:
                img = plt.imread(filepath)
                if img.ndim == 3:
                    img = img[..., 0]   
                features = segment2feature(img)
                
                X_features.append(features.flatten())
                Y_labels.append(4) 
            except Exception as e:
                print(f"Skipping {filename} in {RIGHTBAD_IMAGES_FOLDER}. Error: {e}")
    
    """ # Class 5: Wide bad Pictures
    print(f"Processing images from '{WIDEBAD_IMAGES_FOLDER}' (Class 5 - Wide Bad)...")
    for filename in os.listdir(WIDEBAD_IMAGES_FOLDER):
        if filename.lower().endswith(image_extensions):
            filepath = os.path.join(WIDEBAD_IMAGES_FOLDER, filename)
            try:
                img = plt.imread(filepath)
                if img.ndim == 3:
                    img = img[..., 0]   
                features = segment2feature(img)
                
                X_features.append(features.flatten())
                Y_labels.append(5) 
            except Exception as e:
                print(f"Skipping {filename} in {WIDEBAD_IMAGES_FOLDER}. Error: {e}") """
                
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

def classify_single_image(img):
    """
    Loads a single image, processes it through the feature pipeline, 
    and predicts its class using the trained classifier.
    
    Returns: 0 for 'Bad', 1 for 'Good' (based on your training labels)."""
    try:
        loaded_classifier = load_classifier(CLASSIFIER_FILENAME)
    except FileNotFoundError:
        print(f"🚨 Error: Classifier file '{CLASSIFIER_FILENAME}' not found. Please train the classifier first.")
        exit()
    try:
        seg_img = segment(img)

        features = segment2feature(seg_img)
        
        input_feature_vector = features.flatten().reshape(1, -1)
        
        prediction = loaded_classifier.predict(input_feature_vector)
        
        return str(prediction[0])
        
    except Exception as e:
        print(f"Error processing and classifying image: {e}")
        return None
    
def setupClassifier():
# --- Configuration ---
    ## 1. Load Data and Extract Features
    X, Y = load_and_process_data()
    if len(X) == 0:
        print("\nЁЯЪи ERROR: No features extracted. Check your folder paths and image files.")
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


if __name__ == "__main__": 
    #setupClassifier()

    NEW_IMAGE_PATH = 'test.png'
    TEST_DATA_ROOT = 'processed_data_folder'
    # 1. Load the trained classifier
    try:
        loaded_classifier = load_classifier(CLASSIFIER_FILENAME)
    except FileNotFoundError:
        print(f"🚨 Error: Classifier file '{CLASSIFIER_FILENAME}' not found. Please train the classifier first.")
        exit()

    hit_rate = calculate_hit_rate(TEST_DATA_ROOT, loaded_classifier)
    print(hit_rate)

    """ # --- Example of Loading and Testing ---
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