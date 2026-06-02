using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Fusion.Addons.AnchorsAddon
{
    /// <summary>
    /// Utility class to handles model calibration with anchors.
    /// It provides methods :
    ///     - to load, save & remove calibration data in a json file.
    ///     - to add anchors to an object or get anchors from an object.
    /// </summary>

    [System.Serializable]
    public struct CalibrationAnchor
    {
        public Vector3 position;
        public Quaternion rotation;
        public string id;
    }

    public enum DetectionType
    {
        MarkerDetection = 0,
        PointOfInterestManualDeclaration = 1
    }

    [System.Serializable]
    public struct ModelCalibration
    {
        public string calibrationName;
        public DetectionType calibrationType;
        public string modelName;
        public Vector3 scale;
        public List<CalibrationAnchor> anchors;

        public ModelCalibration(string calibrationName, DetectionType calibrationType = DetectionType.MarkerDetection)
        {
            this.calibrationName = calibrationName;
            scale = Vector3.one;
            modelName = calibrationName;
            anchors = new List<CalibrationAnchor>();
            this.calibrationType = calibrationType;
        }

        public bool TryGetCalibrationAnchorForId(string id, out CalibrationAnchor foundAnchor)
        {
            foundAnchor = default;
            foreach (var a in anchors) {
                if (a.id == id)
                {
                    foundAnchor = a;
                    return true;
                }
            }
            return false;
        }


        #region JSON
        public string ToJson()
        {
            return JsonUtility.ToJson(this, prettyPrint: true);
        }

        public static ModelCalibration FromJson(string json)
        {
            return JsonUtility.FromJson<ModelCalibration>(json);
        }
        #endregion

        #region Save/load

        public static string SaveFolderPath()
        {
            string saveFolderPath = Application.persistentDataPath + Path.DirectorySeparatorChar + "Calibration";
            return saveFolderPath;
        }

        public static string SavePath(DetectionType type, string calibrationName)
        {
            if (string.IsNullOrEmpty(calibrationName))
            {
                throw new System.Exception("Unable to access calibration file path, no calibrationName");
            }
            string saveFilePath = SaveFolderPath() + Path.DirectorySeparatorChar + type + "-" + calibrationName + ".json";
            return saveFilePath;
        }
        public string SavePath()
        {
            return SavePath(calibrationType, calibrationName);
        }

        public void Save()
        {
            if (Directory.Exists(SaveFolderPath()) == false)
            {
                Directory.CreateDirectory(SaveFolderPath());
            }
            string saveFilePath = SavePath();
            File.WriteAllText(saveFilePath, ToJson());
            Debug.Log($"Saved calibration to {saveFilePath}");
        }

        public void RemoveSave()
        {
            string saveFilePath = SavePath(calibrationType, calibrationName);
            if (File.Exists(saveFilePath) == false)
            {
                return;
            }
            File.Delete(saveFilePath);
            Debug.Log($"Deleted calibration at {saveFilePath}");
        }

        public static bool TryLoad(DetectionType type, string calibrationName, out ModelCalibration calibration)
        {
            calibration = default;
            string saveFilePath = SavePath(type, calibrationName);
            if (File.Exists(saveFilePath) == false)
            {
                return false;
            }
            string json = File.ReadAllText(saveFilePath);
            calibration = FromJson(json);
            Debug.Log($"Loaded calibration from {saveFilePath}: {calibration.calibrationName}, {calibration.anchors?.Count ?? 0} anchors");
            return true;
        }

        public static bool TryLoadEscapedName(DetectionType calibrationType, string name, out ModelCalibration calibration)
        {
            var calibrationName = name.Replace(" ", "").Replace("/", "").Replace("(", "").Replace(")", "");

            if (ModelCalibration.TryLoad(calibrationType, calibrationName, out calibration))
            {
                return true;
            }
            else
            {
                return false;
            }
        }
        #endregion

        #region Apply to/update from object
        public void UpdateWithObjectAnchorTags(GameObject positionReferenceObject, Transform anchorsRoot = null)
        {
            if (anchorsRoot == null)
            {
                anchorsRoot = positionReferenceObject.transform;
            }

            UpdateWithObjectAnchorTags(positionReferenceObject, new List<AnchorTag>(positionReferenceObject.GetComponentsInChildren<AnchorTag>()));
        }

        public void UpdateWithObjectAnchorTags(GameObject positionReferenceObject, List<AnchorTag> anchorTags)
        {
            var objectTransform = positionReferenceObject.transform;
            anchors.Clear();
            foreach (var anchorTag in anchorTags)
            {
                var calibrationAnchor = new CalibrationAnchor
                {
                    position = objectTransform.InverseTransformPoint(anchorTag.transform.position),
                    rotation = Quaternion.Inverse(objectTransform.rotation) * anchorTag.transform.rotation,
                    id = anchorTag.anchorId
                };
                anchors.Add(calibrationAnchor);
            }
        }

        public void ApplyToObjectAnchorTags(GameObject rootObject, Transform anchorsRoot = null, bool destroyUnusedAnchors = false, GameObject referenceAnchorPrefab = null)
        {
            var objectTransform = rootObject.transform;
            if(anchorsRoot == null)
            {
                anchorsRoot = objectTransform;
            }
            var unusedAnchorTags = new List<AnchorTag>(rootObject.GetComponentsInChildren<AnchorTag>());
            foreach (var anchor in anchors)
            {
                AnchorTag anchorTag = null;
                foreach(var unusedAnchorTag in unusedAnchorTags)
                {
                    if(unusedAnchorTag.anchorId == anchor.id)
                    {
                        anchorTag = unusedAnchorTag;
                        unusedAnchorTags.Remove(unusedAnchorTag);
                        break;
                    }
                }
                if (anchorTag == null)
                {
                    GameObject anchorTagGO;
                    if(referenceAnchorPrefab == null)
                    {
                        anchorTagGO = new GameObject("Anchor" + anchor.id);
                        anchorTag = anchorTagGO.AddComponent<AnchorTag>();
                    }
                    else
                    {
                        anchorTagGO = GameObject.Instantiate(referenceAnchorPrefab);
                        anchorTag = anchorTagGO.GetComponent<AnchorTag>();
                        if (anchorTag == null) throw new System.Exception("referenceAnchorPrefab should contain a AnchorTag at its root");
                    }
                    anchorTagGO.transform.parent = anchorsRoot;
                    anchorTag.anchorId = anchor.id;
                }
                anchorTag.transform.position = objectTransform.TransformPoint(anchor.position);
                anchorTag.transform.rotation = objectTransform.rotation * anchor.rotation;
            }

            if (destroyUnusedAnchors)
            {
                foreach (var unusedAnchorTag in unusedAnchorTags)
                {
                    Debug.LogError($"Destroy unmapped anchor "+unusedAnchorTag.name);
                    GameObject.Destroy(unusedAnchorTag.gameObject);
                }
            }
        }
        #endregion
    }
}
