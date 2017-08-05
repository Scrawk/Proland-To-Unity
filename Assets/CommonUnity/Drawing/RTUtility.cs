using UnityEngine;
using System.Collections.Generic;

namespace Common.Unity.Drawing
{

    static public class RTUtility
    {

        public static void Blit(RenderTexture des, Material mat, int pass = 0)
        {
            RenderTexture oldRT = RenderTexture.active;

            Graphics.SetRenderTarget(des);

            GL.Clear(true, true, Color.clear);

            GL.PushMatrix();
            GL.LoadOrtho();

            mat.SetPass(pass);

            GL.Begin(GL.QUADS);
            GL.TexCoord2(0.0f, 0.0f); GL.Vertex3(0.0f, 0.0f, 0.1f);
            GL.TexCoord2(1.0f, 0.0f); GL.Vertex3(1.0f, 0.0f, 0.1f);
            GL.TexCoord2(1.0f, 1.0f); GL.Vertex3(1.0f, 1.0f, 0.1f);
            GL.TexCoord2(0.0f, 1.0f); GL.Vertex3(0.0f, 1.0f, 0.1f);
            GL.End();

            GL.PopMatrix();

            RenderTexture.active = oldRT;
        }

        public static void MultiTargetBlit(RenderTexture[] des, Material mat, int pass = 0)
        {
            //RenderTexture oldRT = RenderTexture.active;

            RenderBuffer[] rb = new RenderBuffer[des.Length];

            for (int i = 0; i < des.Length; i++)
                rb[i] = des[i].colorBuffer;

            Graphics.SetRenderTarget(rb, des[0].depthBuffer);

            GL.Clear(true, true, Color.clear);

            GL.PushMatrix();
            GL.LoadOrtho();

            mat.SetPass(pass);

            GL.Begin(GL.QUADS);
            GL.TexCoord2(0.0f, 0.0f); GL.Vertex3(0.0f, 0.0f, 0.1f);
            GL.TexCoord2(1.0f, 0.0f); GL.Vertex3(1.0f, 0.0f, 0.1f);
            GL.TexCoord2(1.0f, 1.0f); GL.Vertex3(1.0f, 1.0f, 0.1f);
            GL.TexCoord2(0.0f, 1.0f); GL.Vertex3(0.0f, 1.0f, 0.1f);
            GL.End();

            GL.PopMatrix();

            //RenderTexture.active = oldRT;
        }

        public static void MultiTargetBlit(RenderBuffer[] des_rb, RenderBuffer des_db, Material mat, int pass = 0)
        {
            //RenderTexture oldRT = RenderTexture.active;

            Graphics.SetRenderTarget(des_rb, des_db);

            GL.Clear(true, true, Color.clear);

            GL.PushMatrix();
            GL.LoadOrtho();

            mat.SetPass(pass);

            GL.Begin(GL.QUADS);
            GL.TexCoord2(0.0f, 0.0f); GL.Vertex3(0.0f, 0.0f, 0.1f);
            GL.TexCoord2(1.0f, 0.0f); GL.Vertex3(1.0f, 0.0f, 0.1f);
            GL.TexCoord2(1.0f, 1.0f); GL.Vertex3(1.0f, 1.0f, 0.1f);
            GL.TexCoord2(0.0f, 1.0f); GL.Vertex3(0.0f, 1.0f, 0.1f);
            GL.End();

            GL.PopMatrix();

            //RenderTexture.active = oldRT;
        }

        public static void ClearColor(IList<RenderTexture> tex, Color col)
        {
            if (tex == null) return;

            for (int i = 0; i < tex.Count; i++)
                ClearColor(tex[i], col);
        }

        public static void ClearColor(RenderTexture tex, Color col)
        {
            if (tex == null) return;

            //RenderTexture oldRT = RenderTexture.active;

            if (!SystemInfo.SupportsRenderTextureFormat(tex.format)) return;

            Graphics.SetRenderTarget(tex);
            GL.Clear(false, true, col);

            //RenderTexture.active = oldRT;
        }

        public static void Swap(RenderTexture[] texs)
        {
            RenderTexture temp = texs[0];
            texs[0] = texs[1];
            texs[1] = temp;
        }
    }
}
