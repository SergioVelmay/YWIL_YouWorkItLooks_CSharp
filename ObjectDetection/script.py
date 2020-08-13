import sys
import os
from argparse import ArgumentParser, SUPPRESS
import onnxruntime
import onnx
import numpy as np
from object_detection import ObjectDetection
from PIL import Image
import tempfile
import cv2

def build_argparser():
    parser = ArgumentParser(add_help=False)
    args = parser.add_argument_group('Options')
    args.add_argument('-h', '--help', action='help', default=SUPPRESS, help='Show this help message and exit.')
    args.add_argument('-i', '--input', help='Required. Path to a folder with images or path to an image files',
                      required=True,
                      type=str, nargs='+')
    return parser

class ONNXRuntimeObjectDetection(ObjectDetection):
    def __init__(self, model_filename, labels):
        super(ONNXRuntimeObjectDetection, self).__init__(labels, 0.5, 5)
        model = onnx.load(model_filename)
        with tempfile.TemporaryDirectory() as dirpath:
            temp = os.path.join(dirpath, os.path.basename(model_filename))
            model.graph.input[0].type.tensor_type.shape.dim[-1].dim_param = 'dim1'
            model.graph.input[0].type.tensor_type.shape.dim[-2].dim_param = 'dim2'
            onnx.save(model, temp)
            options = onnxruntime.SessionOptions()
            options.graph_optimization_level = onnxruntime.GraphOptimizationLevel.ORT_DISABLE_ALL
            self.session = onnxruntime.InferenceSession(temp, options)
        self.input_name = self.session.get_inputs()[0].name
        self.is_fp16 = self.session.get_inputs()[0].type == 'tensor(float16)'
        
    def predict(self, preprocessed_image):
        inputs = np.array(preprocessed_image, dtype=np.float32)[np.newaxis,:,:,(2,1,0)]

        inputs = np.ascontiguousarray(np.rollaxis(inputs, 3, 1))

        if self.is_fp16:
            inputs = inputs.astype(np.float16)

        outputs = self.session.run(None, {self.input_name: inputs})

        return np.squeeze(outputs).transpose((1,2,0)).astype(np.float32)

def main():
    args = build_argparser().parse_args()
    model_path = 'model.onnx'
    model_labels = 'model.labels'

    with open(model_labels, 'r') as f:
        labels = [x.split(sep=' ', maxsplit=1)[-1].strip() for x in f]

    object_detection_model = ONNXRuntimeObjectDetection(model_path, labels)

    image = Image.open(args.input[0])

    predictions = object_detection_model.predict_image(image)
    
    print(predictions)
    
if __name__ == '__main__':
    sys.exit(main() or 0)