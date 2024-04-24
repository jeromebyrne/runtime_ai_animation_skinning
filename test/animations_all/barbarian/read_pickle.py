import pickle
import json
import numpy as np

def custom_converter(obj):
    """Convert non-serializable objects for JSON."""
    if isinstance(obj, np.ndarray):
        return obj.tolist()  # Convert numpy arrays to list
    # Add more conversions if necessary
    raise TypeError(f"Object of type {obj.__class__.__name__} is not JSON serializable")

def pickle_to_json(pickle_file_path, json_file_path):
    # Load data from a pickle file
    with open(pickle_file_path, 'rb') as file:
        data = pickle.load(file)

    # Convert the data to JSON format
    with open(json_file_path, 'w') as file:
        json.dump(data, file, default=custom_converter, indent=4)


pickle_to_json('./float_1713298088.2386503.pkl', 'float.json')
