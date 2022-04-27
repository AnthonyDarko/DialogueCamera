//#if UNITY_EDITOR

using UnityEditor;
using UnityEngine;

namespace Pangu.Tools
{
    public partial class ScreenSpaceCameraSolver
    {
        int drawMode = 0;

        private void DebugInfo()
        {
            var size = 0.001f;
            //Gizmos.DrawSphere(_lookCenter, size);
            //Gizmos.DrawSphere(_bp, size);
            //Gizmos.DrawSphere(_fp, size);
        }

        private void OnDrawGizmos()
        {
            if (!Valid()) { return; }
            drawMode = 1;
            DrawTarget();
            DrawResultPoint(_camera, wbPosition, Color.red, out var bvp);
            DrawResultPoint(_camera, wfPosition, Color.green, out var fvp);
            DrawLine(bvp, fvp);
            DebugInfo();

            //辅助线
            Handles.DrawLine(_bp, _bp + cameraRight * 0.1f);
            Handles.DrawLine(wbPosition, wbPosition + _camera.transform.right * 0.07f);
            //Handles.DrawLine(wbPosition, wbPosition + cameraRight * 0.1f);
            //Handles.DrawLine(wbPosition, wbPosition + Vector3.up * 0.03f);
            var VecWbWf = wfPosition - wbPosition;
            var VecWbN = new Vector3(VecWbWf.x * (float)blPfb, VecWbWf.y * (float)blPfb, VecWbWf.z * (float)blPfb);
            var VecWBK = new Vector3(VecWbN.x, 0, VecWbN.z);
            Handles.DrawLine(wbPosition, wbPosition + VecWbWf);
            //Handles.DrawLine(wbPosition + VecWBK, wbPosition + VecWBK + Vector3.up * 0.05f);
            //Handles.DrawLine(_lookCenter, _lookCenter + (wfPosition - _fp) * 10);
            //Handles.DrawLine(_lookCenter, _lookCenter + Vector3.down * 0.1f);
            //Handles.DrawLine(_fp, _fp + (wfPosition - _fp) * 10);
            Handles.DrawLine(wbPosition, wbPosition + VecWbN * 1f);
            //Handles.Label(wbPosition + VecWbN, "N");
            //Handles.DrawLine(wbPosition + VecWBK, wbPosition + VecWBK + new Vector3(_camera.transform.forward.x, 0, _camera.transform.forward.z) * 0.2f);
            //Handles.DrawLine( new Vector3(_camera.transform.position.x, 0, _camera.transform.position.z), new Vector3(_camera.transform.position.x, 0, _camera.transform.position.z) + new Vector3(_camera.transform.forward.x, 0, _camera.transform.forward.z) * 0.3f);
            Handles.Label(wbPosition + VecWBK, "K");
            //Handles.DrawLine(wbPosition, _camera.transform.position - wbPosition);
            //Handles.DrawLine(wfPosition, wfPosition + (_camera.transform.position - wfPosition));

            //Handles.DrawLine(bvp, bvp + (wfPosition - _fp) * 10);
            //Handles.DrawLine(_lookCenter, _lookCenter + _camera.transform.forward * 1f);
        }

        private void DrawResultPoint(Camera camera, Vector3 position, Color color, out Vector3 nearPos)
        {
            var vp = camera.WorldToViewportPoint(position);
            var cp = camera.transform.position;
            var depth = vp.z;
            var interval = camera.nearClipPlane / depth;
            SetColor(color);
            #region NearPlane
            nearPos = Vector3.Lerp(cp, position, interval);
            var nearCenter = cp + camera.transform.forward * camera.nearClipPlane;
            #endregion
            #region PosPlane
            var posCenter = cp + camera.transform.forward * depth;
            var ppp = posCenter + Vector3.ProjectOnPlane(position - posCenter, camera.transform.up);
            DrawLine(posCenter, cp);
            DrawLine(posCenter, ppp);
            //Handles.DrawLine(posCenter, posCenter + (wfPosition - _fp) * 11 );
            DrawLine(position, ppp);
            #endregion
            SetColor(Color.white);
            Handles.Label(_bp, "B");
            Handles.Label(_fp, "F");
            Handles.Label(wbPosition, "W_b");
            Handles.Label(wfPosition, "W_f");
            Handles.Label(_lookCenter, "L");
            Handles.Label(_lookCenter + camera.transform.forward * (-1.0f) * (float)fl * (float)_cosC, "A");
            Handles.Label(cp, "C");
            Handles.Label((ppp + posCenter) / 2, $"{Mathf.Abs(vp.x * 2 - 1):F2}");
            Handles.Label((ppp + position) / 2, $"{(vp.y - 0.5f):F2}");
            DrawLine(nearCenter, cp);
            DrawLine(wbPosition, wbPosition + _fp - _bp);
        }

        private void DrawTarget()
        {
            //DrawLine(wfPosition, wbPosition);
            DrawLine(_bp, _fp);
            var size = 80f;
            var sv = SceneView.currentDrawingSceneView;
            var camera = _camera;
            if (sv)
            {
                camera = sv.camera;
            }
            Handles.BeginGUI();
            var fsp = camera.WorldToScreenPoint(wfPosition);
            var bsp = camera.WorldToScreenPoint(wbPosition);
            var bSize = size / bsp.z;
            var fSize = bSize / (float)_overSacle;
            //EditorGUI.DrawRect(new Rect(fsp.x - fSize, camera.pixelHeight - fsp.y - 2 * fSize,
            //    fSize * 2, fSize * 2), Color.blue * 0.4f);
            //EditorGUI.DrawRect(new Rect(bsp.x - bSize, camera.pixelHeight - bsp.y - 2 * bSize,
            //    bSize * 2, bSize * 2), Color.blue * 0.4f);
            Handles.EndGUI();
        }

        private void DrawLine(Vector3 p1, Vector3 p2)
        {
            switch (drawMode)
            {
                case 0:
                    Handles.DrawLine(p1, p2);
                    break;
                case 1:
                    Gizmos.DrawLine(p1, p2);
                    break;
            }
        }

        private void SetColor(Color color, float alpha = 1)
        {
            color.a = alpha;
            Gizmos.color = color;
            Handles.color = color;
        }

    }
}

//#endif
