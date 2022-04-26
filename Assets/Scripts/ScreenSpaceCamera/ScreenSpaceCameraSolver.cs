using System;


#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;
using System;
using System.Threading;

namespace Pangu.Tools
{
    [System.Serializable, ExecuteInEditMode]
    public partial class ScreenSpaceCameraSolver : MonoBehaviour
    {
        [System.Serializable]
        public class ViewportTarget
        {
            public Transform target;
            [Range(0, 1)]
            public float compositionX = 0.33f;
            [Range(-0.5f, 0.5f)]
            public float compositionY = 0;
        }

        [SerializeField] private Camera _camera;
        [Space(20)]
        public ViewportTarget front;
        [Space(20)]
        public ViewportTarget back;
        [Range(-90, 90)]
        [Space(20)]
        public float yaw = -30;
        [Range(0.1f, 75)]
        [Space(20)]
        public float fov = 30;
        [Range(-90, 90)]
        [Space(20)]
        public float dutch = 0;

        [SerializeField] private float x_Test;// = _camera.transform.up.y;
        [SerializeField] private float y_Test;
        [SerializeField] private float z_Test;

        [SerializeField] private float x_Right;// = _camera.transform.up.y;
        [SerializeField] private float y_Right;
        [SerializeField] private float z_Right;

        [SerializeField] private float Var1;// = _camera.transform.up.y;
        [SerializeField] private float Var2;
        [SerializeField] private float Var3;

        public double bCompX => back.compositionX;
        public double bCompY => back.compositionY;
        public double fCompX => front.compositionX;
        public double fCompY => front.compositionY;

        public Vector3 wbPosition => back.target.position;
        public Vector3 wfPosition => front.target.position;
        private Transform _calcTarget => _camera.transform;

        private double _aspect;//屏幕长宽比
        private double _overSacle;//wb所在相机深度和wf所在相机深度的投影面大小比
        private Vector3 _lookCenter;
        private Vector3 _bp;//wb在相机水平面的投影点
        private Vector3 _fp;//wf在相机水平面的投影点
        private double _sinC;//偏航角的sin值
        private double _cosC;//偏航角的cos值
        private double _tanHalfHorizonFov;//横向fov的一半的tan值
        private Vector3 cameraRight;
        private Vector3 camUp;
        private Vector3 camFwd;

        public double blPfb;

        private void Update()
        {
            Calculate();
        }

        public Transform Calculate()
        {
            if (!_calcTarget) return null;
            if (!Valid()) return null;
            CalculateCameraPos();
            return _calcTarget;
        }

        private void CalculateCameraPos()
        {
            if (bCompX == 0 || fCompX == 0) return;
            if (bCompX + fCompX == 1) return;

            #region Constant
            var cAngle = Mathf.Abs(yaw);
            _sinC = Mathf.Sin(Mathf.Deg2Rad * cAngle);
            _cosC = Mathf.Cos(Mathf.Deg2Rad * cAngle);
            _aspect = _camera.aspect;
            var tanHalfVerticalFov = Mathf.Tan(Mathf.Deg2Rad * fov / 2);
            _tanHalfHorizonFov = tanHalfVerticalFov * _aspect;
            #endregion

            //bCompX = 1f - 2 * bCompX;
            #region Solve
            //c-相机，wb-背景目标，wf-前景目标，l相机中心线和fb的交点
            //f-前景目标在相机水平面投影，b-背景目标在相机水平面投影
            var clPbl = _sinC / _tanHalfHorizonFov / bCompX - _cosC; // CL / BL，这里的CompX直接采用坐标位置，设中间位置为原点
            var clPfl = _sinC / _tanHalfHorizonFov / fCompX + _cosC; // CL / FL
            blPfb = clPfl / (clPfl + clPbl); // BL / FB
            var flPfb = clPbl / (clPfl + clPbl); // FL / FB
            // 下面这个式子直接写成 flPfb / blPfb 应该也是可以的，不能这么处理，这是空间中的几何，移动情况不同，裁剪空间中的点在进行投影时，压缩前和压缩后的比值可能不同
            //_overSacle = (flPfb / fCompX) / (blPfb / bCompX); // 写成(FL / fCompX) / (BL / bCompX)几何意义为L点到裁剪边界的线段长度的比值(即两个平面的缩放比) 或 (FL / BL) * (bCompX / fCompX) 注意：FL / BL 和 屏幕坐标的比值可能会非常不同，取决于偏航角Yaw
            //对两个面的比值进行重新计算，得到如下算式，假设两个平面与中轴线的交点为F'与B'，直接对CF'和CB'进行化简，得到一下式子
            _overSacle = (1 - _cosC / clPfl) / (1 + _cosC / clPbl); //两个面线段的比值关系，由CompX决定
            
            //CL的值求解
            var x = _sinC / fCompX / _aspect; //tan(FLD)   //_sinC / fCompX * 2 / _aspect; // (这里的常量2可以考虑去掉) 可以约为 [ sinC / (fCompX / 2 * aspect) ] => 2 * tan(DLF)，设中轴线交点为A，下边界交点为D，最后可以得到 2 * FD / FL
            var clPdh2 = clPfl * clPfl / (abs(fCompY - bCompY * _overSacle) * abs(fCompY - bCompY * _overSacle) * x * x); // 这里的对应关系错了，需要重新解算 // bCompY * _overSacle，这是拿到bCompY在fCompY平面上投影的高度，dh的意思是fCompY与bCompY在F的投影面上的高度差
            var clPfb = clPbl * clPfl / (clPfl + clPbl); // CL / FB
            var clPfb2 = clPfb * clPfb; // CL / FB 的平方
            var clPwfwb2 = clPfb2 * clPdh2 / (clPfb2 + clPdh2);
            var wfwb = Vector3.Distance(wbPosition, wfPosition);
            var cl = wfwb * sqrt(clPwfwb2); //这里采用了近似值求解，寻找更精准的方法代替，重新计算clPdh2之后正常

            var fl = cl / clPfl;
            var bl = cl / clPbl;
            var bS = bl * _sinC / bCompX / _aspect * 2; // B点到下边界垂线 * 2
            var fS = fl * _sinC / fCompX / _aspect * 2; // F点到下边界垂线 * 2
            var fY = fS * fCompY;
            var bY = bS * bCompY;
            #endregion

            //不采用迭代的方式，直接进行计算，这个值是否应该由外界决定，由设计者决定上方向的方向，这里设计者应该给出一个场景的上方向（废弃）
            #region CalUp
            //利用三射线定理，由相机与物体B连线，可以得到CWB与WBWF的夹角，由bY与CWB的数值关系，可以拿到他们的夹角，再由cos(WBWFF)可以拿到FWBB的余弦值，由三射线定理拿到摄像机上方向与面C-WB-WF的夹角
            //利用二面角，做三次旋转
            //上述想法废弃，改用Pitch，Yaw对WBWF向量的变化情况进行描述，在变化过程中，WBWF的旋转情况如何，转轴是什么向量？

            //【解决思路】
            //先由L点做FWF的平行线，交WBWF于点N，得到一个相似三角形，由BL/FB的比值与向量WBWF相乘得到向量WBN
            //从N点向X-Z水平面做垂线得到垂足即投影点K，此时可以得到B'K向量，由于WBB'与B'LN面垂直，所以B'K向量同时与NK向量和WBB'向量垂直
            //此时WBK向量可以由向量WBN得到，即直接去掉y轴坐标，由此，可以得到WBK向量，WBB'向量，B'K向量，三者在长度上符合勾股定理
            //至此，拿到cos(K_WB_B')的值，即世界坐标系下，相机偏航角的补角的余弦值，让WBK向量绕Y轴旋转该角度拿到相机的右方向向量
            //此时，再由WBB'向量与WBN拿到B'N向量，再通过B'N，B'L，LN三者的数量关系拿到角LB'N，使B'N绕相机右方向旋转角度LB'N拿到向量B'L
            //将B'L与向量WBB'叉乘拿到相机的上方向向量
            if (true)
            {
                var VecWbWf = wfPosition - wbPosition;
                var VecWbN = new Vector3(VecWbWf.x * (float)blPfb, VecWbWf.y * (float)blPfb, VecWbWf.z * (float)blPfb);
                var VecWBK = new Vector3(VecWbN.x, 0, VecWbN.z);
                // B'L = BL * cos(Yaw), LN = fY * blPfb, B'N = sqrt(B'L * B'L + LN * LN), B'K = sqrt(B'N * B'N - VecWbN.y * VecWbN.y)
                var BdotL = bl * abs(_cosC); //平移之后得到的L点并不在BdotNK平面上，所以才有平移后的L点离原L点越远误差越大的情况

                //LN的值需要根据fY与bY的空间位置关系来判断，有两者同向和两者异向的情况
                double LN;
                LN = abs(fY - bY) * blPfb;
                //LN = sqrt(VecWbN.magnitude * VecWbN.magnitude - bl * _sinC * bl * _sinC); //bl的值是会变化的
                //if (fCompY * bCompY < 0)
                //{
                //    LN = abs((abs(fY) + abs(bY)) * blPfb - abs(bY));
                //}
                //else
                //{
                //    LN = (abs(fY) - abs(bY)) * blPfb;
                //}

                var BdotN2 = (LN * LN + bl * _cosC * bl * _cosC);//VecWbN.sqrMagnitude - bl * _sinC * bl * _sinC; //BdotL * BdotL + LN * LN; //这里的计算方式需要改变，
                //var BdotK = sqrt(BdotN2 - VecWbN.y * VecWbN.y);
                //var SinBdotWBK = BdotK / (VecWBK.magnitude);
                //var BdotWBK = Mathf.Asin((float)SinBdotWBK) * Mathf.Rad2Deg;

                //var BdotWB = sqrt(bl * bl - BdotL * BdotL) ;//abs(bl * _sinC); 
                //float CosBdotWBK = (float)BdotWB / (VecWBK.magnitude); //夹角的Cos值应该由向量除以两个向量的模才能得到，这里的计算方式有问题，Why?不能用这种方式进行计算，寻找其他数值方法计算
                //var BdotWBK = Mathf.Acos(CosBdotWBK) * Mathf.Rad2Deg;
                var WBBdot = bl * abs(_sinC);

                var BdotK2 = BdotN2 - VecWbN.y * VecWbN.y;
                var BdotK = sqrt(BdotK2);
                var SinBdotWBK = (float)BdotK / sqrt((float)Vector3.Dot(VecWBK, VecWBK));
                var BdotWBK = Mathf.Asin((float)SinBdotWBK) * Mathf.Rad2Deg;

                //判断VecWBBdot与VecWBK的位置关系
                {
                    if (yaw < 0)
                    {
                        cameraRight = (Quaternion.AngleAxis(-BdotWBK, Vector3.up) * VecWBK).normalized; //这个向量用于后续计算向上向量，需要注意一下方向问题，或者改变偏航角的计算方式
                    }
                    else if (yaw > 0)
                    {
                        cameraRight = (Quaternion.AngleAxis(BdotWBK, Vector3.up) * -VecWBK).normalized;
                    }
                }

                Var1 = (float)(VecWbN.sqrMagnitude - bl * _sinC * bl * _sinC);// sqrt(bl * bl - BdotL * BdotL);// VecWBK.magnitude;// (Mathf.Asin((float)_sinC) * Mathf.Rad2Deg);// BdotL;// (float)(VecWbN.sqrMagnitude - bl * _sinC * bl * _sinC);
                Var2 = (float)(LN * LN + bl * _cosC * bl * _cosC);// abs(bl * _sinC);// bl;  // (Mathf.Asin((float)(BdotWB / bl)) * Mathf.Rad2Deg);// (bl * _sinC);// (BdotL * BdotL + LN * LN);
                Var3 = (float)0;   // (bl * _sinC);

                //判断VecWBBdot与CameraRight的位置关系
                Vector3 VecWBBdot;
                {
                    if (yaw > 0)
                    {
                        VecWBBdot = (-1.0f) * cameraRight.normalized * (float)WBBdot;
                    }
                    else
                    {
                        VecWBBdot = cameraRight.normalized * (float)WBBdot;
                    }
                }

                var VecBdotK = (-1.0f) * (VecWBBdot) + VecWBK;
                //var KBdotN = Mathf.Acos((float)(BdotK / sqrt(BdotN2))) * Mathf.Rad2Deg; //KBdotN的角度不是实际的转角，还需要加上LBdotN，拿到角KBdotL
                var LBdotN = Mathf.Acos((float)(BdotL / sqrt(BdotN2))) * Mathf.Rad2Deg;
                var VecBdotN = VecWbN - VecWBBdot;
                //var LBdotK = abs((float)KBdotN) + abs((float)LBdotN); //不需要这个角度，直接旋转VecBdotN KBdotN度即可
                //判断VecBdotN与VecLBdot（即camFwd）的位置关系
                {
                    if (abs(fY) > abs(bY))
                    {
                        camFwd = Quaternion.AngleAxis((float)LBdotN * (1.0f), (1.0f * cameraRight)) * -VecBdotN.normalized;
                    }
                    else
                    {
                        camFwd = Quaternion.AngleAxis((float)LBdotN * (-1.0f), (1.0f * cameraRight)) * -VecBdotN.normalized;
                    }
                }

                camUp = Vector3.Cross(camFwd, cameraRight);

                //迭代部分
                var upAxis = _camera.transform.up; // camUp;//
                x_Test = _camera.transform.right.normalized.x;
                y_Test = _camera.transform.right.normalized.y;
                z_Test = _camera.transform.right.normalized.z;
                _bp = wbPosition - upAxis * (float)(bY);
                _fp = wfPosition - upAxis * (float)(fY);
                _lookCenter = _bp * (float)flPfb + _fp * (float)blPfb;
                var cameraFwd =  Quaternion.AngleAxis(-yaw, upAxis) * (_bp - _fp).normalized; //camFwd;//
                //////////////////////////

                x_Right = cameraRight.x;
                y_Right = cameraRight.y;
                z_Right = cameraRight.z;

                #endregion

                #region Apply
                _calcTarget.transform.position = _lookCenter -
                    (float)(cl) * cameraFwd;
                _calcTarget.transform.LookAt(_lookCenter, Quaternion.Euler(0, 0, dutch) * Vector3.up);
                _camera.fieldOfView = fov;
                #endregion
            }

        }

        private bool Valid()
        {
            bool isValid = _calcTarget && !PositionEquals(wfPosition, wbPosition);
            if (!isValid)
            {
                Debug.LogError($"Solver not valid {(_calcTarget == null)} ftPos: {wfPosition} btPos: {wbPosition}");
            }
            return isValid;
        }

        private bool PositionEquals(Vector3 a, Vector3 b)
        {
            return Vector3.Distance(a, b) < 0.01f;
        }

        double abs(double x)
        {
            return x < 0 ? -x : x;
        }

        double sqrt(double x)
        {
            return Mathf.Sqrt((float)x);
        }

    }
}
