using OpenCVForUnity.Calib3dModule;
using OpenCVForUnity.CoreModule;
using OpenCVForUnity.ImgprocModule;
using OpenCVForUnity.ObjdetectModule;
using OpenCVForUnity.UnityUtils;
using OpenCVForUnity.UnityUtils.Helper;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.XR.CoreUtils;
using UnityEngine;

namespace TryAR.MarkerTracking
{
    /// <summary>
    /// ArUco marker detection and tracking component.
    /// Handles detection of ArUco markers in camera frames and provides pose estimation.
    /// </summary>
    public class ArUcoMarkerTracking : MonoBehaviour
    {
        /// <summary>
        /// The ArUco dictionary to use for marker detection.
        /// </summary>
        [SerializeField] private ArUcoDictionary _dictionaryId = ArUcoDictionary.DICT_4X4_50;

        [Space(10)]

        /// <summary>
        /// The length of the markers' side in meters.
        /// </summary>
        [SerializeField] public float _markerLength = 0.1f;

        public float MarkerLength { get { return _markerLength; } set { _markerLength = value; } }

        /// <summary>
        /// Coefficient for low-pass filter (0-1). Higher values mean more smoothing.
        /// </summary>
        [Range(0, 1)]
        [SerializeField] private float _poseFilterCoefficient = 0.5f;

        /// <summary>
        /// Division factor for input image resolution. Higher values improve performance but reduce detection accuracy.
        /// </summary>
        [SerializeField] private int _divideNumber = 2;
        
        /// <summary>
        /// Read-only access to the divide number value
        /// </summary>
        public int DivideNumber => _divideNumber;

        // OpenCV matrices for image processing
        /// <summary>
        /// RGB format mat for marker detection and result display.
        /// </summary>
        private Mat _processingRgbMat;

        /// <summary>
        /// Full-size RGBA mat from original webcam image.
        /// </summary>
        private Mat _originalWebcamMat;
        
        /// <summary>
        /// Resized mat for intermediate processing.
        /// </summary>
        private Mat _halfSizeMat;

        /// <summary>
        /// The camera intrinsic parameters matrix.
        /// </summary>
        private Mat _cameraIntrinsicMatrix;

        /// <summary>
        /// The camera distortion coefficients.
        /// </summary>
        private MatOfDouble _cameraDistortionCoeffs;

        // ArUco detection related mats and variables
        private Mat _detectedMarkerIds;
        private List<Mat> _detectedMarkerCorners;
        private List<Mat> _rejectedMarkerCandidates;
        private Dictionary markerDictionary;
        private Mat recoveredMarkerIndices;
        private ArucoDetector arucoDetector;

        private bool _isReady = false;

        public bool trackingProcessInProgress = false;

        /// <summary>
        /// Read-only access to determine if tracking is ready
        /// </summary>
        public bool IsReady => _isReady;

        /// <summary>
        /// Dictionary storing previous pose data for each marker ID for smoothing
        /// </summary>
        private Dictionary<int, PoseData> _prevPoseDataDictionary = new Dictionary<int, PoseData>();

        public float PoseFilterCoefficient { get => _poseFilterCoefficient; set => _poseFilterCoefficient = value; }

        /// <summary>
        /// Initialize the marker tracking system with camera parameters
        /// </summary>
        /// <param name="imageWidth">Camera image width in pixels</param>
        /// <param name="imageHeight">Camera image height in pixels</param>
        /// <param name="cx">Principal point X coordinate</param>
        /// <param name="cy">Principal point Y coordinate</param>
        /// <param name="fx">Focal length X</param>
        /// <param name="fy">Focal length Y</param>
        public void Initialize(int imageWidth, int imageHeight, float cx, float cy, float fx, float fy)
        {
            InitializeMatrices(imageWidth, imageHeight, cx, cy, fx, fy);        
        }

        /// <summary>
        /// Initialize all OpenCV matrices and detector parameters
        /// </summary>
        private void InitializeMatrices(int originalWidth, int originalHeight, float cX, float cY, float fX, float fY)
        {            
            // Processing dimensions (scaled by divide number)
            int processingWidth = originalWidth / _divideNumber;
            int processingHeight = originalHeight / _divideNumber;
            fX = fX / _divideNumber;
            fY = fY / _divideNumber;
            cX = cX / _divideNumber;
            cY = cY / _divideNumber;

            // Create camera intrinsic matrix
            _cameraIntrinsicMatrix = new Mat(3, 3, CvType.CV_64FC1);
            _cameraIntrinsicMatrix.put(0, 0, fX);
            _cameraIntrinsicMatrix.put(0, 1, 0);
            _cameraIntrinsicMatrix.put(0, 2, cX);
            _cameraIntrinsicMatrix.put(1, 0, 0);
            _cameraIntrinsicMatrix.put(1, 1, fY);
            _cameraIntrinsicMatrix.put(1, 2, cY);
            _cameraIntrinsicMatrix.put(2, 0, 0);
            _cameraIntrinsicMatrix.put(2, 1, 0);
            _cameraIntrinsicMatrix.put(2, 2, 1.0f);

            // No distortion coefficients for Quest cameras
            _cameraDistortionCoeffs = new MatOfDouble(0, 0, 0, 0);

            // Initialize all processing mats
            _originalWebcamMat = new Mat(originalHeight, originalWidth, CvType.CV_8UC4);
            _halfSizeMat = new Mat(processingHeight, processingWidth, CvType.CV_8UC4);
            _processingRgbMat = new Mat(processingHeight, processingWidth, CvType.CV_8UC3);

            // Create ArUco detection mats
            _detectedMarkerIds = new Mat();
            _detectedMarkerCorners = new List<Mat>();
            _rejectedMarkerCandidates = new List<Mat>();
            markerDictionary = Objdetect.getPredefinedDictionary((int)_dictionaryId);
            recoveredMarkerIndices = new Mat();
            
            // Configure detector parameters for optimal performance
            DetectorParameters detectorParams = new DetectorParameters();
            detectorParams.set_minDistanceToBorder(3);
            detectorParams.set_useAruco3Detection(true);
            detectorParams.set_cornerRefinementMethod(Objdetect.CORNER_REFINE_SUBPIX);
            detectorParams.set_minSideLengthCanonicalImg(20);
            detectorParams.set_errorCorrectionRate(0.8);
            RefineParameters refineParameters = new RefineParameters(10f, 3f, true);

            // Create the ArUco detector
            arucoDetector = new ArucoDetector(markerDictionary, detectorParams, refineParameters);

            _isReady = true;
        }

        /// <summary>
        /// Release all OpenCV resources
        /// </summary>
        private void ReleaseResources()
        {
            Debug.Log("Releasing ArUco tracking resources");

            if (_processingRgbMat != null)
                _processingRgbMat.Dispose();

            if (_originalWebcamMat != null)
                _originalWebcamMat.Dispose();
                
            if (_halfSizeMat != null)
                _halfSizeMat.Dispose();
  
            if (arucoDetector != null)
                arucoDetector.Dispose();

            if (_detectedMarkerIds != null)
                _detectedMarkerIds.Dispose();

            if (_detectedMarkerCorners != null)
            {
                foreach (var corner in _detectedMarkerCorners)
                {
                    corner.Dispose();
                }
                _detectedMarkerCorners.Clear();
            }

            if(_rejectedMarkerCandidates != null)
            {
                foreach (var rejectedCorner in _rejectedMarkerCandidates)
                {
                    rejectedCorner.Dispose();
                }
                _rejectedMarkerCandidates.Clear();
            }

            if (recoveredMarkerIndices != null)
                recoveredMarkerIndices.Dispose();
        }

        /// <summary>
        /// Handle errors that occur during tracking operations
        /// </summary>
        /// <param name="errorCode">Error code</param>
        /// <param name="message">Error message</param>
        public void HandleError(Source2MatHelperErrorCode errorCode, string message)
        {
            Debug.Log("ArUco tracking error: " + errorCode + ":" + message);
        }


        public async Task ProcessMarkerTracking(WebCamTexture webCamTexture, Texture2D resultTexture, Dictionary<int, GameObject> arObjects, Transform camTransform)
        {
            if(_isReady == false || webCamTexture == null || trackingProcessInProgress == true)
            {
                return;
            }

            trackingProcessInProgress = true;

            // Step 1: Convert webcam texture to OpenCV mat
            // Get image from webcam at full size

            // Background thread equivalent of Utils.webCamTextureToMat(webCamTexture, _originalWebcamMat);
            var imageBytes = await ImageProcessing.GetTextureBytes(webCamTexture, TextureFormat.RGBA32);
            int flipCode = 0;
            bool flip = true;
            MatUtils.copyToMat(imageBytes, _originalWebcamMat);
            if (flip)
            {
                Core.flip(_originalWebcamMat, _originalWebcamMat, flipCode);
            }

            var mainThreadScheduler = TaskScheduler.FromCurrentSynchronizationContext();

            //await Task.Delay(20);
            float start = Time.realtimeSinceStartup;
            await Task.Run(() => {
                // Background thread
                // Step 2: Detect ArUco markers in the current camera frame
                DoDetectMarker(resultTexture);

                // Step 3: Estimate the pose of markers and position 3D objects accordingly
                // This maps the 2D marker positions to 3D space using the camera parameters
                EstimatePoseCanonicalMarker(arObjects, camTransform);
            });

            trackingProcessInProgress = false;
        }

        /// <summary>
        /// Detect ArUco markers in the provided webcam texture
        /// </summary>
        /// <param name="webCamTexture">Input webcam texture</param>
        /// <param name="resultTexture">Optional output texture for visualization</param>
        public void DetectMarker(WebCamTexture webCamTexture, Texture2D resultTexture = null)
        {
            if (_isReady)
            {
                if (webCamTexture == null)
                {
                    return;
                }

                // Get image from webcam at full size
                Utils.webCamTextureToMat(webCamTexture, _originalWebcamMat);

                DoDetectMarker(resultTexture);
            }
        }

        /// <summary>
        /// Detect ArUco markers in the provided webcam texture
        /// </summary>
        /// <param name="webCamTexture">Input webcam texture</param>
        /// <param name="resultTexture">Optional output texture for visualization</param>
        void DoDetectMarker(Texture2D resultTexture = null)
        {
            if (_isReady)
            {
                // Resize for processing
                Imgproc.resize(_originalWebcamMat, _halfSizeMat, _halfSizeMat.size());
                
                // Convert to RGB for ArUco processing
                Imgproc.cvtColor(_halfSizeMat, _processingRgbMat, Imgproc.COLOR_RGBA2RGB);

              
                // Reset detection containers
                _detectedMarkerIds.create(0, 1, CvType.CV_32S);
                _detectedMarkerCorners.Clear();
                _rejectedMarkerCandidates.Clear();
                
                // Detect markers
                arucoDetector.detectMarkers(_processingRgbMat, _detectedMarkerCorners, _detectedMarkerIds, _rejectedMarkerCandidates);
                
                // Draw detected markers for visualization
                if (_detectedMarkerCorners.Count == _detectedMarkerIds.total() || _detectedMarkerIds.total() == 0){
                    Objdetect.drawDetectedMarkers(_processingRgbMat, _detectedMarkerCorners, _detectedMarkerIds, new Scalar(0, 255, 0));
                }
                        
                 

                // Update result texture for visualization
                if (resultTexture != null)
                {
                    Utils.matToTexture2D(_processingRgbMat, resultTexture);
                }
            }
        }

        /// <summary>
        /// Estimate pose for each detected marker and update corresponding game objects
        /// </summary>
        /// <param name="arObjects">Dictionary mapping marker IDs to game objects</param>
        /// <param name="camTransform">Camera transform for world-space positioning</param>
        public void EstimatePoseCanonicalMarker(Dictionary<int, GameObject> arObjects, Transform camTransform)
        {
            // Skip if not ready or no markers detected
            if (!_isReady || _detectedMarkerCorners == null || _detectedMarkerCorners.Count == 0)
            {
                if (_isReady)
                {
                    foreach (var kvp in arObjects)
                    {
                        GameObject obj = kvp.Value;
                        // Deactivate the object
                        if (obj != null)
                        {
                            obj.SetActive(false);
                        }
                    }
                }

                return;
            }
            DetectXROrigin();

            // Define 3D coordinates of marker corners (marker center is at origin)
            using (MatOfPoint3f objectPoints = new MatOfPoint3f(
                new Point3(-_markerLength / 2f, _markerLength / 2f, 0),
                new Point3(_markerLength / 2f, _markerLength / 2f, 0),
                new Point3(_markerLength / 2f, -_markerLength / 2f, 0),
                new Point3(-_markerLength / 2f, -_markerLength / 2f, 0)
            ))
            {
                // Process each detected marker
                for (int i = 0; i < _detectedMarkerCorners.Count; i++)
                {
                    // Get marker ID
                    int currentMarkerId = (int)_detectedMarkerIds.get(i, 0)[0];
                    
                    // Check if this marker has a corresponding game object
                    if (!arObjects.TryGetValue(currentMarkerId, out GameObject targetObject) || targetObject == null)
                        continue;
                    
                    using (Mat rotationVec = new Mat(1, 1, CvType.CV_64FC3))
                    using (Mat translationVec = new Mat(1, 1, CvType.CV_64FC3))
                    using (Mat corner_4x1 = _detectedMarkerCorners[i].reshape(2, 4))
                    using (MatOfPoint2f imagePoints = new MatOfPoint2f(corner_4x1))
                    {
                        // Solve PnP to get marker pose
                        Calib3d.solvePnP(objectPoints, imagePoints, _cameraIntrinsicMatrix, _cameraDistortionCoeffs, rotationVec, translationVec);
                        
                        // Convert to Unity coordinate system
                        double[] rvecArr = new double[3];
                        rotationVec.get(0, 0, rvecArr);
                        double[] tvecArr = new double[3];
                        translationVec.get(0, 0, tvecArr);
                        PoseData poseData = ARUtils.ConvertRvecTvecToPoseData(rvecArr, tvecArr);

                        // Get previous pose for this marker (or create new)
                        if (!_prevPoseDataDictionary.TryGetValue(currentMarkerId, out PoseData prevPose))
                        {
                            prevPose = new PoseData();
                            _prevPoseDataDictionary[currentMarkerId] = prevPose;
                        }

                        // Apply low-pass filter if we have previous pose data
                        if (prevPose.pos != Vector3.zero)
                        {
                            float t = _poseFilterCoefficient;
                            
                            // Filter position with linear interpolation
                            poseData.pos = Vector3.Lerp(poseData.pos, prevPose.pos, t);
                            
                            // Filter rotation with spherical interpolation
                            poseData.rot = Quaternion.Slerp(poseData.rot, prevPose.rot, t);
                        }
                        
                        // Store current pose for next frame
                        _prevPoseDataDictionary[currentMarkerId] = poseData;

                        // Convert pose to matrix and apply to game object
                        var arMatrix = ARUtils.ConvertPoseDataToMatrix(ref poseData, true);
                        arMatrix = camTransform.localToWorldMatrix * arMatrix;
                        ARUtils.SetTransformFromMatrix(targetObject.transform, ref arMatrix);
                        if (rigOrigin)
                        {
                            // The targetObject has been positioned around a rig placed at O,O,O. We adapt if the rig moved
                            var position = rigOrigin.transform.TransformPoint(targetObject.transform.position);
                            var rotation = rigOrigin.transform.rotation * targetObject.transform.rotation;
                            //var position = targetObject.transform.position - origin.transform.position;
                            targetObject.transform.rotation = rotation;
                            targetObject.transform.position = position;
                        }
                    }
                }

                // Optional feature to deactivate objects for markers that weren't detected
                // (Use only if required by your application)
                foreach (var kvp in arObjects)
                {
                    int markerId = kvp.Key;
                    GameObject obj = kvp.Value;

                    // Check if this marker was detected in this frame
                    bool markerDetectedThisFrame = false;
                    for (int i = 0; i < _detectedMarkerIds.total(); i++)
                    {
                        if ((int)_detectedMarkerIds.get(i, 0)[0] == markerId)
                        {
                            markerDetectedThisFrame = true;
                            break;
                        }
                    }

                    // Deactivate the object if the marker wasn't detected
                    if (markerDetectedThisFrame == false && obj != null)
                    {
                        obj.SetActive(false);
                    }
                    if (markerDetectedThisFrame && obj != null && obj.activeSelf == false)
                    {
                        obj.SetActive(true);
                    }
                }
            }
        }

        public Transform rigOrigin;
        void DetectXROrigin()
        {
            if (rigOrigin == null)
            {
                var xrOrigin = FindAnyObjectByType<XROrigin>(FindObjectsInactive.Exclude);
                if (xrOrigin != null)
                {
                    rigOrigin = xrOrigin.transform;
                }
            }
        }

        /// <summary>
        /// Explicitly release resources when the object is disposed
        /// </summary>
        public void Dispose()
        {
            ReleaseResources();
        }

        /// <summary>
        /// Clean up when object is destroyed
        /// </summary>
        void OnDestroy()
        {
            ReleaseResources();
        }

        /// <summary>
        /// Type of ArUco marker to detect
        /// </summary>
        public enum MarkerType
        {
            CanonicalMarker,
            GridBoard,
            ChArUcoBoard,
            ChArUcoDiamondMarker
        }

        /// <summary>
        /// Available ArUco dictionaries for marker detection
        /// </summary>
        public enum ArUcoDictionary
        {
            DICT_4X4_50 = Objdetect.DICT_4X4_50,
            DICT_4X4_100 = Objdetect.DICT_4X4_100,
            DICT_4X4_250 = Objdetect.DICT_4X4_250,
            DICT_4X4_1000 = Objdetect.DICT_4X4_1000,
            DICT_5X5_50 = Objdetect.DICT_5X5_50,
            DICT_5X5_100 = Objdetect.DICT_5X5_100,
            DICT_5X5_250 = Objdetect.DICT_5X5_250,
            DICT_5X5_1000 = Objdetect.DICT_5X5_1000,
            DICT_6X6_50 = Objdetect.DICT_6X6_50,
            DICT_6X6_100 = Objdetect.DICT_6X6_100,
            DICT_6X6_250 = Objdetect.DICT_6X6_250,
            DICT_6X6_1000 = Objdetect.DICT_6X6_1000,
            DICT_7X7_50 = Objdetect.DICT_7X7_50,
            DICT_7X7_100 = Objdetect.DICT_7X7_100,
            DICT_7X7_250 = Objdetect.DICT_7X7_250,
            DICT_7X7_1000 = Objdetect.DICT_7X7_1000,
            DICT_ARUCO_ORIGINAL = Objdetect.DICT_ARUCO_ORIGINAL,
        }
    }
}