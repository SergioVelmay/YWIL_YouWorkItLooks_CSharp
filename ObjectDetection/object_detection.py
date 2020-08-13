import numpy as np
import math

class ObjectDetection(object):
    ANCHORS = np.array([[0.573, 0.677], [1.87, 2.06], [3.34, 5.47], [7.88, 3.53], [9.77, 9.17]])
    IOU_THRESHOLD = 0.4
    DEFAULT_INPUT_SIZE = 416 * 416

    def __init__(self, labels, prob_threshold = 0.5, max_detections = 5):
        assert len(labels) >= 1

        self.labels = labels
        self.prob_threshold = prob_threshold
        self.max_detections = max_detections

    def _logistic(self, x):
        return np.where(x > 0, 1 / (1 + np.exp(-x)), np.exp(x) / (1 + np.exp(x)))

    def _non_maximum_suppression(self, boxes, class_probs, max_detections):
        assert len(boxes) == len(class_probs)

        max_detections = min(max_detections, len(boxes))
        max_probs = np.amax(class_probs, axis=1)
        max_classes = np.argmax(class_probs, axis=1)

        areas = boxes[:, 2] * boxes[:, 3]

        selected_boxes = []
        selected_classes = []
        selected_probs = []

        while len(selected_boxes) < max_detections:
            i = np.argmax(max_probs)
            if max_probs[i] < self.prob_threshold:
                break

            selected_boxes.append(boxes[i])
            selected_classes.append(max_classes[i])
            selected_probs.append(max_probs[i])

            box = boxes[i]
            other_indices = np.concatenate((np.arange(i), np.arange(i + 1, len(boxes))))
            other_boxes = boxes[other_indices]

            x1 = np.maximum(box[0], other_boxes[:, 0])
            y1 = np.maximum(box[1], other_boxes[:, 1])
            x2 = np.minimum(box[0] + box[2], other_boxes[:, 0] + other_boxes[:, 2])
            y2 = np.minimum(box[1] + box[3], other_boxes[:, 1] + other_boxes[:, 3])
            w = np.maximum(0, x2 - x1)
            h = np.maximum(0, y2 - y1)

            overlap_area = w * h
            iou = overlap_area / (areas[i] + areas[other_indices] - overlap_area)

            overlapping_indices = other_indices[np.where(iou > self.IOU_THRESHOLD)[0]]
            overlapping_indices = np.append(overlapping_indices, i)

            class_probs[overlapping_indices, max_classes[i]] = 0
            max_probs[overlapping_indices] = np.amax(class_probs[overlapping_indices], axis=1)
            max_classes[overlapping_indices] = np.argmax(class_probs[overlapping_indices], axis=1)

        assert len(selected_boxes) == len(selected_classes) and len(selected_boxes) == len(selected_probs)
        return selected_boxes, selected_classes, selected_probs

    def _extract_bb(self, prediction_output, anchors):
        assert len(prediction_output.shape) == 3
        num_anchor = anchors.shape[0]
        height, width, channels = prediction_output.shape
        assert channels % num_anchor == 0

        num_class = int(channels / num_anchor) - 5
        assert num_class == len(self.labels)

        outputs = prediction_output.reshape((height, width, num_anchor, -1))

        x = (self._logistic(outputs[..., 0]) + np.arange(width)[np.newaxis, :, np.newaxis]) / width
        y = (self._logistic(outputs[..., 1]) + np.arange(height)[:, np.newaxis, np.newaxis]) / height
        w = np.exp(outputs[..., 2]) * anchors[:, 0][np.newaxis, np.newaxis, :] / width
        h = np.exp(outputs[..., 3]) * anchors[:, 1][np.newaxis, np.newaxis, :] / height

        x = x - w / 2
        y = y - h / 2
        boxes = np.stack((x, y, w, h), axis=-1).reshape(-1, 4)

        objectness = self._logistic(outputs[..., 4])

        class_probs = outputs[..., 5:]
        class_probs = np.exp(class_probs - np.amax(class_probs, axis=3)[..., np.newaxis])
        class_probs = class_probs / np.sum(class_probs, axis=3)[..., np.newaxis] * objectness[..., np.newaxis]
        class_probs = class_probs.reshape(-1, num_class)

        assert len(boxes) == len(class_probs)
        return (boxes, class_probs)

    def predict_image(self, image):
        inputs = self.preprocess(image)
        prediction_outputs = self.predict(inputs)
        return self.postprocess(prediction_outputs)

    def preprocess(self, image):
        image = image.convert("RGB") if image.mode != "RGB" else image
        ratio = math.sqrt(self.DEFAULT_INPUT_SIZE / image.width / image.height)
        new_width = int(image.width * ratio)
        new_height = int(image.height * ratio)
        new_width = 32 * round(new_width / 32)
        new_height = 32 * round(new_height / 32)
        image = image.resize((new_width, new_height))
        return image

    def postprocess(self, prediction_outputs):
        boxes, class_probs = self._extract_bb(prediction_outputs, self.ANCHORS)

        max_probs = np.amax(class_probs, axis=1)
        index, = np.where(max_probs > self.prob_threshold)
        index = index[(-max_probs[index]).argsort()]

        selected_boxes, selected_classes, selected_probs = self._non_maximum_suppression(boxes[index],
                                                                                         class_probs[index],
                                                                                         self.max_detections)

        return [{'label': self.labels[selected_classes[i]],
                 'probability': str(round(float(100 * selected_probs[i]), 1)),
                 'box': {
                     'left': round(float(selected_boxes[i][0]), 8),
                     'top': round(float(selected_boxes[i][1]), 8),
                     'width': round(float(selected_boxes[i][2]), 8),
                     'height': round(float(selected_boxes[i][3]), 3)}
                 } for i in range(len(selected_boxes))]
