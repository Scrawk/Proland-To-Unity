using System.Collections;
using Common.Mathematics.LinearAlgebra;

namespace Common.Geometry.Shapes
{
    public enum FRUSTUM_VISIBILTY { FULLY = 0, PARTIALLY = 1, INVISIBLE = 3 };

    public class Frustum3d
    {

        Vector4d[] Planes { get; set; }

        public Frustum3d()
        {
            Planes = new Vector4d[6];
        }

        public Frustum3d(Matrix4x4d mat)
        {
            Planes = new Vector4d[6];
            SetPlanes(Planes, mat);
        }

        public void SetPlanes(Matrix4x4d mat)
        {
            SetPlanes(Planes, mat);
        }

        public static void SetPlanes(Vector4d[] Planes, Matrix4x4d mat)
        {
            //extract frustum planes from a projection matrix

            // Extract the LEFT plane
            Planes[0] = new Vector4d();
            Planes[0].x = mat.m30 + mat.m00;
            Planes[0].y = mat.m31 + mat.m01;
            Planes[0].z = mat.m32 + mat.m02;
            Planes[0].w = mat.m33 + mat.m03;
            // Extract the RIGHT plane
            Planes[1] = new Vector4d();
            Planes[1].x = mat.m30 - mat.m00;
            Planes[1].y = mat.m31 - mat.m01;
            Planes[1].z = mat.m32 - mat.m02;
            Planes[1].w = mat.m33 - mat.m03;
            // Extract the BOTTOM plane
            Planes[2] = new Vector4d();
            Planes[2].x = mat.m30 + mat.m10;
            Planes[2].y = mat.m31 + mat.m11;
            Planes[2].z = mat.m32 + mat.m12;
            Planes[2].w = mat.m33 + mat.m13;
            // Extract the TOP plane
            Planes[3] = new Vector4d();
            Planes[3].x = mat.m30 - mat.m10;
            Planes[3].y = mat.m31 - mat.m11;
            Planes[3].z = mat.m32 - mat.m12;
            Planes[3].w = mat.m33 - mat.m13;
            // Extract the NEAR plane
            Planes[4] = new Vector4d();
            Planes[4].x = mat.m30 + mat.m20;
            Planes[4].y = mat.m31 + mat.m21;
            Planes[4].z = mat.m32 + mat.m22;
            Planes[4].w = mat.m33 + mat.m23;
            // Extract the FAR plane
            Planes[5] = new Vector4d();
            Planes[5].x = mat.m30 - mat.m20;
            Planes[5].y = mat.m31 - mat.m21;
            Planes[5].z = mat.m32 - mat.m22;
            Planes[5].w = mat.m33 - mat.m23;

        }

        public FRUSTUM_VISIBILTY GetVisibility(Box3d box)
        {
            return GetVisibility(Planes, box);
        }

        public static FRUSTUM_VISIBILTY GetVisibility(Vector4d[] Planes, Box3d box)
        {

            FRUSTUM_VISIBILTY v0 = GetVisibility(Planes[0], box);
            if (v0 == FRUSTUM_VISIBILTY.INVISIBLE)
            {
                return FRUSTUM_VISIBILTY.INVISIBLE;
            }

            FRUSTUM_VISIBILTY v1 = GetVisibility(Planes[1], box);
            if (v1 == FRUSTUM_VISIBILTY.INVISIBLE)
            {
                return FRUSTUM_VISIBILTY.INVISIBLE;
            }

            FRUSTUM_VISIBILTY v2 = GetVisibility(Planes[2], box);
            if (v2 == FRUSTUM_VISIBILTY.INVISIBLE)
            {
                return FRUSTUM_VISIBILTY.INVISIBLE;
            }

            FRUSTUM_VISIBILTY v3 = GetVisibility(Planes[3], box);
            if (v3 == FRUSTUM_VISIBILTY.INVISIBLE)
            {
                return FRUSTUM_VISIBILTY.INVISIBLE;
            }

            FRUSTUM_VISIBILTY v4 = GetVisibility(Planes[4], box);
            if (v4 == FRUSTUM_VISIBILTY.INVISIBLE)
            {
                return FRUSTUM_VISIBILTY.INVISIBLE;
            }

            if (v0 == FRUSTUM_VISIBILTY.FULLY && v1 == FRUSTUM_VISIBILTY.FULLY &&
                v2 == FRUSTUM_VISIBILTY.FULLY && v3 == FRUSTUM_VISIBILTY.FULLY &&
                v4 == FRUSTUM_VISIBILTY.FULLY)
            {
                return FRUSTUM_VISIBILTY.FULLY;
            }

            return FRUSTUM_VISIBILTY.PARTIALLY;
        }

        private static FRUSTUM_VISIBILTY GetVisibility(Vector4d clip, Box3d box)
        {
            double x0 = box.Min.x * clip.x;
            double x1 = box.Max.x * clip.x;
            double y0 = box.Min.y * clip.y;
            double y1 = box.Max.y * clip.y;
            double z0 = box.Min.z * clip.z + clip.w;
            double z1 = box.Max.z * clip.z + clip.w;
            double p1 = x0 + y0 + z0;
            double p2 = x1 + y0 + z0;
            double p3 = x1 + y1 + z0;
            double p4 = x0 + y1 + z0;
            double p5 = x0 + y0 + z1;
            double p6 = x1 + y0 + z1;
            double p7 = x1 + y1 + z1;
            double p8 = x0 + y1 + z1;

            if (p1 <= 0 && p2 <= 0 && p3 <= 0 && p4 <= 0 && p5 <= 0 && p6 <= 0 && p7 <= 0 && p8 <= 0)
            {
                return FRUSTUM_VISIBILTY.INVISIBLE;
            }
            if (p1 > 0 && p2 > 0 && p3 > 0 && p4 > 0 && p5 > 0 && p6 > 0 && p7 > 0 && p8 > 0)
            {
                return FRUSTUM_VISIBILTY.FULLY;
            }
            return FRUSTUM_VISIBILTY.PARTIALLY;
        }

    }

}
