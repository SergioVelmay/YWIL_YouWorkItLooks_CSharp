#!/usr/bin/env python

import sys
import os
from argparse import ArgumentParser, SUPPRESS
import cv2
import numpy as np
import json
from openvino.inference_engine import IECore
import cv2

def build_argparser():
    parser = ArgumentParser(add_help=False)
    args = parser.add_argument_group('Options')
    args.add_argument('-h', '--help', action='help', default=SUPPRESS, help='Show this help message and exit.')
    args.add_argument('-i', '--input', help='Required. Path to a folder with images or path to an image files',
                      required=True,
                      type=str, nargs='+')
    return parser

class Result:
    def __init__(self, label, probability):
        self.label = label
        self.probability = probability

def main():
    args = build_argparser().parse_args()
    model_path = 'model'
    model_xml = model_path + '.xml'
    model_bin = model_path + '.bin'
    model_labels = model_path + '.labels'
    device_name = 'CPU'
    number_top = 1

    inference_engine = IECore()

    network = inference_engine.read_network(model=model_xml, weights=model_bin)

    input_blob = next(iter(network.input_info))
    output_blob = next(iter(network.outputs))
    network.batch_size = len(args.input)

    n, c, h, w = network.input_info[input_blob].input_data.shape
    images = np.ndarray(shape=(n, c, h, w))
    for i in range(n):
        image = cv2.imread(args.input[i])
        if image.shape[:-1] != (h, w):
            image = cv2.resize(image, (w, h))
        image = image.transpose((2, 0, 1))
        images[i] = image

    exec_network = inference_engine.load_network(network=network, device_name=device_name)

    res = exec_network.infer(inputs={input_blob: images})

    res = res[output_blob]

    with open(model_labels, 'r') as f:
        labels = [x.split(sep=' ', maxsplit=1)[-1].strip() for x in f]
    
    for i, probs in enumerate(res):
        probs = np.squeeze(probs)
        top_ind = np.argsort(probs)[-number_top:][::-1]
        for id in top_ind:
            prob = '{:.3f}'.format(probs[id] * 100)
            result = Result(labels[id], prob)

    data = json.dumps(result.__dict__, indent=4)

    print(data)

if __name__ == '__main__':
    sys.exit(main() or 0)
