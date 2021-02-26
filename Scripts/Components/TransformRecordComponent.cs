﻿using System.Collections.Generic;
using UnityEngine;

namespace PlayRecorder
{

    [System.Serializable]
    public class TransformFrame : RecordFrame
    {
        // Used in recording thread
        public Vector3 localPosition;
        public Quaternion localRotation;
        public Vector3 localScale;

        public TransformFrame(int tick, TransformCache tc) : base(tick)
        {
            localPosition = tc.localPosition;
            localRotation = tc.localRotation;
            localScale = tc.localScale;
        }
    }

    public class TransformCache
    {
        private Transform _transform;

        // Used in main thread
        public Vector3 localPosition;
        public Quaternion localRotation;
        public Vector3 localScale;

        public bool hasChanged = false;

        public TransformCache(Transform transform)
        {
            this._transform = transform;
            localPosition = transform.localPosition;
            localRotation = transform.localRotation;
            localScale = transform.localScale;
        }

        public void Update()
        {
            hasChanged = false;
            if (_transform.localPosition != localPosition)
            {
                localPosition = _transform.localPosition;
                hasChanged = true;
            }
            if (_transform.localRotation != localRotation)
            {
                localRotation = _transform.localRotation;
                hasChanged = true;
            }
            if (_transform.localScale != localScale)
            {
                localScale = _transform.localScale;
                hasChanged = true;
            }
        }

    }

    [AddComponentMenu("PlayRecorder/RecordComponents/TransformRecordComponent")]
    public class TransformRecordComponent : RecordComponent
    {

        [SerializeField, Tooltip("Automatically assigned to the current object transform, changes will be ignored and reset once recording starts.")]
        protected Transform _baseTransform = null;

        [SerializeField]
        protected List<Transform> _extraTransforms = new List<Transform>();

        protected List<TransformCache> _transformCache = new List<TransformCache>();

        #region Unity Events

#if UNITY_EDITOR
        private void OnValidate()
        {
            _baseTransform = gameObject.transform;
        }

        protected override void Reset()
        {
            base.Reset();
            _baseTransform = gameObject.transform;
        }
#endif

        #endregion

        #region Recording

        public override void StartRecording()
        {
            base.StartRecording();

            _baseTransform = gameObject.transform;

            _transformCache.Clear();
            _transformCache.Add(new TransformCache(_baseTransform));
            for (int i = 0; i < _extraTransforms.Count; i++)
            {
                if (_extraTransforms[i] == null)
                    continue;

                TransformCache tc = new TransformCache(_extraTransforms[i]);
                _transformCache.Add(tc);

            }

            for (int i = 0; i < _transformCache.Count; i++)
            {
                RecordPart rp = new RecordPart();
                rp.AddFrame(new TransformFrame(_currentTick, _transformCache[i]));
                _recordItem.parts.Add(rp);
            }
        }

        protected override void RecordUpdateLogic()
        {
            for (int i = 0; i < _transformCache.Count; i++)
            {
                _transformCache[i].Update();
            }
        }

        protected override void RecordTickLogic()
        {
            for (int i = 0; i < _transformCache.Count; i++)
            {
                if (_transformCache[i].hasChanged)
                {
                    _transformCache[i].hasChanged = false;
                    _recordItem.parts[i].AddFrame(new TransformFrame(_currentTick, _transformCache[i]));
                }
            }
        }

        protected override void OnRecordingEnable()
        {
            for (int i = 0; i < _transformCache.Count; i++)
            {
                _transformCache[i].Update();
                _transformCache[i].hasChanged = true;
            }
        }

        #endregion

        #region Playback

        protected override void SetPlaybackIgnoreTransforms()
        {
            _extraTransforms.Clear();
            if(_baseTransform != null)
            {
                _playbackIgnoreTransforms.Add(_baseTransform);
            }
            for (int i = 0; i < _extraTransforms.Count; i++)
            {
                _playbackIgnoreTransforms.Add(_extraTransforms[i]);
            }
        }

        protected override PlaybackIgnoreItem SetDefaultPlaybackIgnores(string type)
        {
            PlaybackIgnoreItem pbi = new PlaybackIgnoreItem(type);
            pbi.disableVRCamera = true;
            pbi.enabledComponents.Add("UnityEngine.UI.");
            return pbi;
        }

        protected override void PlayUpdateLogic()
        {
            for (int i = 0; i < _playUpdatedParts.Count; i++)
            {
                switch (_playUpdatedParts[i])
                {
                    case 0:
                        if (_baseTransform != null && _recordItem.parts[0].currentFrame != null)
                            ApplyTransform((TransformFrame)_recordItem.parts[0].currentFrame, _baseTransform);
                        break;
                    default:
                        if (_extraTransforms[_playUpdatedParts[i] - 1] != null && _recordItem.parts[_playUpdatedParts[i]].currentFrame != null)
                            ApplyTransform((TransformFrame)_recordItem.parts[_playUpdatedParts[i]].currentFrame, _extraTransforms[_playUpdatedParts[i] - 1]);
                        break;
                }
            }
        }

        #endregion

        private void ApplyTransform(TransformFrame frame, Transform transform)
        {
            try
            {
                transform.localPosition = frame.localPosition;
                transform.localRotation = frame.localRotation;
                transform.localScale = frame.localScale;
            }
            catch
            {
                Debug.LogWarning("Transform unable to be updated on " + name + " at tick " + _currentTick.ToString());
            }
        }

        private void DisableAllComponents(Transform transform)
        {
            Behaviour[] behaviours = transform.GetComponents<Behaviour>();
            for (int i = 0; i < behaviours.Length; i++)
            {
                // This may need more items to be added
                if (!(typeof(RecordComponent).IsSameOrSubclass(behaviours[i].GetType()) ||
                   behaviours[i].GetType() == typeof(Renderer) ||
                   behaviours[i].GetType() == typeof(MeshFilter) ||
                   behaviours[i].GetType() == typeof(Camera) ||
                   behaviours[i].GetType() == typeof(Canvas) ||
                   behaviours[i].GetType().ToString().Contains("UnityEngine.UI.")
                   ))
                {
                    (behaviours[i]).enabled = false;
                }
                if (behaviours[i].GetType() == typeof(Camera))
                {
                    ((Camera)behaviours[i]).stereoTargetEye = StereoTargetEyeMask.None;
                }
            }
            Component[] components = transform.GetComponents<Component>();
            for (int i = 0; i < components.Length; i++)
            {
                if (components[i].GetType() == typeof(Rigidbody))
                {
                    ((Rigidbody)components[i]).isKinematic = true;
                }
                if (components[i].GetType() == typeof(Rigidbody2D))
                {
                    ((Rigidbody2D)components[i]).isKinematic = true;
                }
            }
        }
    }

}