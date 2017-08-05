using UnityEngine;
using System.Collections.Generic;

using Common.Mathematics.LinearAlgebra;

namespace Common.Unity.Mathematics
{
	public static class MathConverter
	{
		public static Vector2 ToVector2(Vector2f v)
		{
			return new Vector2(v.x, v.y);
		}

		public static Vector2 ToVector2(Vector2d v)
		{
			return new Vector2((float)v.x, (float)v.y);
		}

		public static Vector2f ToVector2f(Vector2 v)
		{
			return new Vector2f(v.x, v.y);
		}

		public static Vector2d ToVector2d(Vector2 v)
		{
			return new Vector2d(v.x, v.y);
		}

		public static Vector3 ToVector3(Vector3f v)
		{
			return new Vector3(v.x, v.y, v.z);
		}

        public static Vector3 ToVector3(Vector4f v)
        {
            return new Vector3(v.x, v.y, v.z);
        }

        public static Vector3 ToVector3(Vector3d v)
		{
			return new Vector3((float)v.x, (float)v.y, (float)v.z);
		}

        public static Vector3 ToVector3(Vector4d v)
        {
            return new Vector3((float)v.x, (float)v.y, (float)v.z);
        }

        public static Vector3f ToVector3f(Vector3 v)
		{
			return new Vector3f(v.x, v.y, v.z);
		}
		
		public static Vector3d ToVector3d(Vector3 v)
		{
			return new Vector3d(v.x, v.y, v.z);
		}

		public static Vector4 ToVector4(Vector4f v)
		{
			return new Vector4(v.x, v.y, v.z, v.w);
		}

		public static Vector4 ToVector4(Vector4d v)
		{
			return new Vector4((float)v.x, (float)v.y, (float)v.z, (float)v.w);
		}

        public static Vector4 ToVector4(Vector3d v)
        {
            return new Vector4((float)v.x, (float)v.y, (float)v.z, 1.0f);
        }

        public static Vector4 ToVector4(Vector3f v)
        {
            return new Vector4(v.x, v.y, v.z, 1.0f);
        }

        public static Vector4f ToVector4f(Vector4 v)
		{
			return new Vector4f(v.x, v.y, v.z, v.w);
		}
		
		public static Vector4d ToVector4d(Vector4 v)
		{
			return new Vector4d(v.x, v.y, v.z, v.w);
		}

		public static Quaternion ToQuaternion(Quaternion3f q)
		{
			return new Quaternion(q.x, q.y, q.z, q.w);
		}
		
		public static Quaternion ToQuaternion(Quaternion3d q)
		{
			return new Quaternion((float)q.x, (float)q.y, (float)q.z, (float)q.w);
		}
		
		public static Quaternion3f ToQuaternion3f(Quaternion q)
		{
			return new Quaternion3f(q.x, q.y, q.z, q.w);
		}
		
		public static Quaternion3d ToQuaternion3d(Quaternion q)
		{
			return new Quaternion3d(q.x, q.y, q.z, q.w);
		}

		public static Matrix4x4 ToMatrix4x4(Matrix4x4f m)
		{
			Matrix4x4 mat = new Matrix4x4();
			
			mat.m00 = m.m00; mat.m01 = m.m01; mat.m02 = m.m02; mat.m03 = m.m03;
			mat.m10 = m.m10; mat.m11 = m.m11; mat.m12 = m.m12; mat.m13 = m.m13;
			mat.m20 = m.m20; mat.m21 = m.m21; mat.m22 = m.m22; mat.m23 = m.m23;
			mat.m30 = m.m30; mat.m31 = m.m31; mat.m32 = m.m32; mat.m33 = m.m33;
			
			return mat;
		}

        public static Matrix4x4 ToMatrix4x4(Matrix3x3f m)
        {
            Matrix4x4 mat = new Matrix4x4();

            mat.m00 = m.m00; mat.m01 = m.m01; mat.m02 = m.m02; mat.m03 = 0.0f;
            mat.m10 = m.m10; mat.m11 = m.m11; mat.m12 = m.m12; mat.m13 = 0.0f;
            mat.m20 = m.m20; mat.m21 = m.m21; mat.m22 = m.m22; mat.m23 = 0.0f;
            mat.m30 = 0.0f; mat.m31 = 0.0f; mat.m32 = 0.0f; mat.m33 = 0.0f;

            return mat;
        }

        public static Matrix4x4 ToMatrix4x4(Matrix4x4d m)
		{
			Matrix4x4 mat = new Matrix4x4();
			
			mat.m00 = (float)m.m00; mat.m01 = (float)m.m01; mat.m02 = (float)m.m02; mat.m03 = (float)m.m03;
			mat.m10 = (float)m.m10; mat.m11 = (float)m.m11; mat.m12 = (float)m.m12; mat.m13 = (float)m.m13;
			mat.m20 = (float)m.m20; mat.m21 = (float)m.m21; mat.m22 = (float)m.m22; mat.m23 = (float)m.m23;
			mat.m30 = (float)m.m30; mat.m31 = (float)m.m31; mat.m32 = (float)m.m32; mat.m33 = (float)m.m33;
			
			return mat;
		}

        public static Matrix4x4 ToMatrix4x4(Matrix3x3d m)
        {
            Matrix4x4 mat = new Matrix4x4();

            mat.m00 = (float)m.m00; mat.m01 = (float)m.m01; mat.m02 = (float)m.m02; mat.m03 = 0.0f;
            mat.m10 = (float)m.m10; mat.m11 = (float)m.m11; mat.m12 = (float)m.m12; mat.m13 = 0.0f;
            mat.m20 = (float)m.m20; mat.m21 = (float)m.m21; mat.m22 = (float)m.m22; mat.m23 = 0.0f;
            mat.m30 = 0.0f; mat.m31 = 0.0f; mat.m32 = 0.0f; mat.m33 = 0.0f;

            return mat;
        }

        public static Matrix4x4f ToMatrix4x4f(Matrix4x4 mat)
		{
			Matrix4x4f m = new Matrix4x4f();
			
			m.m00 = mat.m00; m.m01 = mat.m01; m.m02 = mat.m02; m.m03 = mat.m03;
			m.m10 = mat.m10; m.m11 = mat.m11; m.m12 = mat.m12; m.m13 = mat.m13;
			m.m20 = mat.m20; m.m21 = mat.m21; m.m22 = mat.m22; m.m23 = mat.m23;
			m.m30 = mat.m30; m.m31 = mat.m31; m.m32 = mat.m32; m.m33 = mat.m33;
			
			return m;
		}

		public static Matrix4x4d ToMatrix4x4d(Matrix4x4 mat)
		{
			Matrix4x4d m = new Matrix4x4d();
			
			m.m00 = mat.m00; m.m01 = mat.m01; m.m02 = mat.m02; m.m03 = mat.m03;
			m.m10 = mat.m10; m.m11 = mat.m11; m.m12 = mat.m12; m.m13 = mat.m13;
			m.m20 = mat.m20; m.m21 = mat.m21; m.m22 = mat.m22; m.m23 = mat.m23;
			m.m30 = mat.m30; m.m31 = mat.m31; m.m32 = mat.m32; m.m33 = mat.m33;
			
			return m;
		}

        public static IList<Vector3> ToVector3(IList<Vector3f> list)
        {
            Vector3[] vectors = new Vector3[list.Count];
            for (int i = 0; i < list.Count; i++)
                vectors[i] = new Vector3(list[i].x, list[i].y, list[i].z);

            return vectors;
        }

        public static IList<Vector3> ToVector3(IList<Vector3d> list)
        {
            Vector3[] vectors = new Vector3[list.Count];
            for (int i = 0; i < list.Count; i++)
                vectors[i] = new Vector3((float)list[i].x, (float)list[i].y, (float)list[i].z);

            return vectors;
        }

        public static IList<Vector3f> ToVector3f(IList<Vector3> list)
        {
            Vector3f[] vectors = new Vector3f[list.Count];
            for (int i = 0; i < list.Count; i++)
                vectors[i] = new Vector3f(list[i].x, list[i].y, list[i].z);

            return vectors;
        }

        public static IList<Vector3d> ToVector3d(IList<Vector3> list)
        {
            Vector3d[] vectors = new Vector3d[list.Count];
            for (int i = 0; i < list.Count; i++)
                vectors[i] = new Vector3d(list[i].x, list[i].y, list[i].z);

            return vectors;
        }

        public static IList<Vector4> ToVector4(IList<Vector4f> list)
        {
            Vector4[] vectors = new Vector4[list.Count];
            for (int i = 0; i < list.Count; i++)
                vectors[i] = new Vector4(list[i].x, list[i].y, list[i].z, list[i].w);

            return vectors;
        }

        public static IList<Vector4> ToVector4(IList<Vector4d> list)
        {
            Vector4[] vectors = new Vector4[list.Count];
            for (int i = 0; i < list.Count; i++)
                vectors[i] = new Vector4((float)list[i].x, (float)list[i].y, (float)list[i].z, (float)list[i].w);

            return vectors;
        }

        public static IList<Vector4f> ToVector4f(IList<Vector4> list)
        {
            Vector4f[] vectors = new Vector4f[list.Count];
            for (int i = 0; i < list.Count; i++)
                vectors[i] = new Vector4f(list[i].x, list[i].y, list[i].z, list[i].w);

            return vectors;
        }

        public static IList<Vector4d> ToVector4d(IList<Vector4> list)
        {
            Vector4d[] vectors = new Vector4d[list.Count];
            for (int i = 0; i < list.Count; i++)
                vectors[i] = new Vector4d(list[i].x, list[i].y, list[i].z, list[i].w);

            return vectors;
        }

    }
}
























