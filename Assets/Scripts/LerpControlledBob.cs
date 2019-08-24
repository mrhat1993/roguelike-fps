using System;
using System.Collections;
using UnityEngine;

    [Serializable]
    public class LerpControlledBob
    {
        public float BobDuration = 0.2f;
        public float BobAmount = 0.1f;

        private float _offset = 0f;


        // provides the offset that can be used
        public float Offset()
        {
            return _offset;
        }


        public IEnumerator DoBobCycle()
        {
            // make the camera move down slightly
            float t = 0f;
            while (t < BobDuration)
            {
                _offset = Mathf.Lerp(0f, BobAmount, t/BobDuration);
                t += Time.deltaTime;
                yield return new WaitForFixedUpdate();
            }

            // make it move back to neutral
            t = 0f;
            while (t < BobDuration)
            {
                _offset = Mathf.Lerp(BobAmount, 0f, t/BobDuration);
                t += Time.deltaTime;
                yield return new WaitForFixedUpdate();
            }
            _offset = 0f;
        }
    }
