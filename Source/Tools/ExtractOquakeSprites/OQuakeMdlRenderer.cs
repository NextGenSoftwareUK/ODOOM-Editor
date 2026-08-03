// OQuakeMdlRenderer — software rasterizer for Quake 1 MDL models.
// Renders the first frame of a model in a front-facing orthographic projection,
// with UV-mapped skin texture and simple Lambert diffuse lighting.
// This produces a proper 3D "game screenshot" sprite rather than a raw skin crop.
//
// Output: BGRA byte array (Format32bppArgb memory layout) at the requested render size.
//
// MDL format reference:
//   http://quakeone.com/forums/quake-help/model-editing-help/4820
//   Quake engine source (gl_mesh.c, r_alias.c)

using System;
using System.Collections.Generic;

namespace ExtractOquakeSprites
{
    internal static class OQuakeMdlRenderer
    {
        // ── Public API ────────────────────────────────────────────────────────────

        /// <summary>
        /// Render the first frame of a Quake MDL from a BGRA skin and parsed geometry.
        /// </summary>
        /// <param name="skin">BGRA skin pixels (width × height × 4 bytes).</param>
        /// <param name="skinW">Skin texture width.</param>
        /// <param name="skinH">Skin texture height.</param>
        /// <param name="verts">World-space vertex positions decoded from the MDL frame.</param>
        /// <param name="stverts">UV coords in skin pixel space, one per vertex. stverts[i] = (s, t, onseam).</param>
        /// <param name="triangles">Triangle list: each entry = (facesfront, v0, v1, v2).</param>
        /// <param name="renderW">Output image width.</param>
        /// <param name="renderH">Output image height.</param>
        /// <returns>BGRA byte array renderW × renderH × 4.</returns>
        public static byte[] Render(
            byte[] skin, int skinW, int skinH,
            float[] verts,         // flat [x0,y0,z0, x1,y1,z1, ...]
            int[][] stverts,       // [i] = {s, t, onseam}
            int[][] triangles,     // [i] = {facesfront, v0, v1, v2}
            int renderW, int renderH)
        {
            // ── Project vertices into screen space ────────────────────────────────
            // Front view: Quake coords — X = right, Y = forward (into screen), Z = up.
            // For a front view we map X→screenX, Z→screenY (flip Z so +Z = up on screen).
            int nv = verts.Length / 3;
            float minX = float.MaxValue, maxX = float.MinValue;
            float minZ = float.MaxValue, maxZ = float.MinValue;
            for (int i = 0; i < nv; i++)
            {
                float wx = verts[i * 3];
                float wz = verts[i * 3 + 2];
                if (wx < minX) minX = wx;
                if (wx > maxX) maxX = wx;
                if (wz < minZ) minZ = wz;
                if (wz > maxZ) maxZ = wz;
            }
            float spanX = maxX - minX;
            float spanZ = maxZ - minZ;
            if (spanX < 0.001f) spanX = 0.001f;
            if (spanZ < 0.001f) spanZ = 0.001f;

            // Uniform scale to fit with 4 px padding, preserving aspect.
            float padFrac = 0.04f;
            int pad = (int)(Math.Min(renderW, renderH) * padFrac);
            float usableW = renderW - 2 * pad;
            float usableH = renderH - 2 * pad;
            float scaleUniform = Math.Min(usableW / spanX, usableH / spanZ);

            // Screen-space positions (float).
            float[] sx = new float[nv];
            float[] sy = new float[nv];
            float[] sz_depth = new float[nv]; // Quake Y = depth (used for z-buffer)
            for (int i = 0; i < nv; i++)
            {
                float wx = verts[i * 3];
                float wy = verts[i * 3 + 1]; // depth
                float wz = verts[i * 3 + 2]; // up
                sx[i] = (wx - minX) * scaleUniform + pad;
                sy[i] = (maxZ - wz) * scaleUniform + pad; // flip Z so top of model is top of image
                sz_depth[i] = wy;
            }

            // ── Allocate output & z-buffer ────────────────────────────────────────
            byte[] outBgra = new byte[renderW * renderH * 4];
            float[] zbuf   = new float[renderW * renderH];
            for (int i = 0; i < zbuf.Length; i++) zbuf[i] = float.MinValue;

            // ── Rasterize triangles ───────────────────────────────────────────────
            int ntris = triangles.Length;
            for (int ti = 0; ti < ntris; ti++)
            {
                int facesfront = triangles[ti][0];
                int i0 = triangles[ti][1];
                int i1 = triangles[ti][2];
                int i2 = triangles[ti][3];

                // Compute screen-space normal to cull back-faces.
                float ax = sx[i1] - sx[i0], ay = sy[i1] - sy[i0];
                float bx = sx[i2] - sx[i0], by = sy[i2] - sy[i0];
                float cross = ax * by - ay * bx;
                if (cross >= 0f) continue; // back-face (negative cross = CW = facing front in screen space)

                // UV coords — apply seam offset for back-facing skin verts.
                float u0 = GetU(stverts, skinW, i0, facesfront == 1);
                float v0 = GetV(stverts, skinH, i0);
                float u1 = GetU(stverts, skinW, i1, facesfront == 1);
                float v1 = GetV(stverts, skinH, i1);
                float u2 = GetU(stverts, skinW, i2, facesfront == 1);
                float v2 = GetV(stverts, skinH, i2);

                // Simple Lambert shading: dot(face normal, front light).
                // Face normal in world (Quake) space: use X/Y/Z of the triangle.
                float nx = (verts[i1 * 3] - verts[i0 * 3]);
                float ny = (verts[i1 * 3 + 1] - verts[i0 * 3 + 1]);
                float nz = (verts[i1 * 3 + 2] - verts[i0 * 3 + 2]);
                float ex = (verts[i2 * 3] - verts[i0 * 3]);
                float ey = (verts[i2 * 3 + 1] - verts[i0 * 3 + 1]);
                float ez = (verts[i2 * 3 + 2] - verts[i0 * 3 + 2]);
                float fnx = ny * ez - nz * ey;
                float fny = nz * ex - nx * ez;
                float fnz = nx * ey - ny * ex;
                float fnLen = (float)Math.Sqrt(fnx * fnx + fny * fny + fnz * fnz);
                if (fnLen > 0.001f) { fnx /= fnLen; fny /= fnLen; fnz /= fnLen; }
                // Light direction: slightly from front-left-up (-0.3, -1, 0.5) in Quake space.
                const float lx = -0.3f, ly = -1.0f, lz = 0.5f;
                const float lLen = 1.162f; // precomputed length of the light dir
                float ldot = (fnx * lx + fny * ly + fnz * lz) / lLen;
                float shade = 0.35f + 0.65f * Math.Max(0f, ldot); // ambient 0.35, diffuse 0.65

                RasterizeTriangle(
                    outBgra, zbuf, renderW, renderH,
                    skin, skinW, skinH,
                    sx[i0], sy[i0], sz_depth[i0], u0, v0,
                    sx[i1], sy[i1], sz_depth[i1], u1, v1,
                    sx[i2], sy[i2], sz_depth[i2], u2, v2,
                    shade);
            }

            return outBgra;
        }

        // ── Parse helpers for Program.cs ─────────────────────────────────────────

        /// <summary>
        /// Decode MDL frame 0 vertex positions using header scale and scale_origin.
        /// Returns flat float[] [x0,y0,z0, x1,y1,z1, ...].
        /// </summary>
        public static float[] DecodeFrameVerts(byte[] mdl, int numVerts, float[] scale, float[] scaleOrigin, int frameVertsOffset)
        {
            float[] verts = new float[numVerts * 3];
            for (int i = 0; i < numVerts; i++)
            {
                int off = frameVertsOffset + i * 4;
                verts[i * 3]     = (mdl[off]     * scale[0]) + scaleOrigin[0];
                verts[i * 3 + 1] = (mdl[off + 1] * scale[1]) + scaleOrigin[1];
                verts[i * 3 + 2] = (mdl[off + 2] * scale[2]) + scaleOrigin[2];
            }
            return verts;
        }

        /// <summary>
        /// Parse stvert array from MDL bytes (12 bytes each: onseam s t).
        /// Returns int[numVerts][3]: {onseam, s, t}.
        /// </summary>
        public static int[][] ParseStVerts(byte[] mdl, int offset, int numVerts)
        {
            int[][] sv = new int[numVerts][];
            for (int i = 0; i < numVerts; i++)
            {
                int p = offset + i * 12;
                sv[i] = new int[]
                {
                    BitConverter.ToInt32(mdl, p),
                    BitConverter.ToInt32(mdl, p + 4),
                    BitConverter.ToInt32(mdl, p + 8)
                };
            }
            return sv;
        }

        /// <summary>
        /// Parse triangle array from MDL bytes (16 bytes each: facesfront v0 v1 v2).
        /// Returns int[numTris][4]: {facesfront, v0, v1, v2}.
        /// </summary>
        public static int[][] ParseTriangles(byte[] mdl, int offset, int numTris)
        {
            int[][] tris = new int[numTris][];
            for (int i = 0; i < numTris; i++)
            {
                int p = offset + i * 16;
                tris[i] = new int[]
                {
                    BitConverter.ToInt32(mdl, p),
                    BitConverter.ToInt32(mdl, p + 4),
                    BitConverter.ToInt32(mdl, p + 8),
                    BitConverter.ToInt32(mdl, p + 12)
                };
            }
            return tris;
        }

        /// <summary>
        /// Compute byte offset to the first frame's vertex data in the MDL.
        /// Walks past skins, stverts, and triangles to reach frame data.
        /// Returns -1 on parse failure.
        /// </summary>
        public static int FindFrameVertsOffset(byte[] mdl, int numskins, int skinwidth, int skinheight, int numverts, int numtris, int numframes, out int stvertsOffset, out int trisOffset)
        {
            stvertsOffset = -1;
            trisOffset    = -1;
            int pos = 84; // sizeof mdl header

            // Skip skins
            int skinSize = skinwidth * skinheight;
            for (int s = 0; s < numskins; s++)
            {
                if (pos + 4 > mdl.Length) return -1;
                int type = BitConverter.ToInt32(mdl, pos);
                pos += 4;
                if (type == 0) // single skin
                {
                    pos += skinSize;
                }
                else // group skin
                {
                    if (pos + 4 > mdl.Length) return -1;
                    int ng = BitConverter.ToInt32(mdl, pos);
                    pos += 4 + ng * 4 + ng * skinSize;
                }
                if (pos > mdl.Length) return -1;
            }

            stvertsOffset = pos;
            pos += numverts * 12; // stvert_t: 3 × int32
            if (pos > mdl.Length) return -1;

            trisOffset = pos;
            pos += numtris * 16; // dtriangle_t: 4 × int32
            if (pos > mdl.Length) return -1;

            // Frames: read first frame (type + bboxmin + bboxmax + name[16] = 4+4+4+16 = 28 bytes header)
            if (pos + 4 > mdl.Length) return -1;
            int frameType = BitConverter.ToInt32(mdl, pos);
            pos += 4;

            if (frameType == 0) // single frame
            {
                pos += 4 + 4 + 16; // bboxmin (4) + bboxmax (4) + name[16]
                if (pos > mdl.Length) return -1;
                return pos; // first vertex
            }
            else // group of frames
            {
                if (pos + 4 > mdl.Length) return -1;
                int ng = BitConverter.ToInt32(mdl, pos);
                pos += 4;
                pos += ng * 4; // times
                // first sub-frame header
                pos += 4 + 4 + 16;
                if (pos > mdl.Length) return -1;
                return pos;
            }
        }

        // ── Internal rasterizer ───────────────────────────────────────────────────

        private static float GetU(int[][] sv, int skinW, int vi, bool facesfront)
        {
            int onseam = sv[vi][0];
            int s = sv[vi][1];
            if (onseam != 0 && !facesfront)
                s += skinW / 2;
            return (s + 0.5f) / skinW;
        }

        private static float GetV(int[][] sv, int skinH, int vi)
        {
            int t = sv[vi][2];
            return (t + 0.5f) / skinH;
        }

        private static void RasterizeTriangle(
            byte[] outBgra, float[] zbuf, int W, int H,
            byte[] skin, int sw, int sh,
            float ax, float ay, float az, float au, float av,
            float bx, float by, float bz, float bu, float bv,
            float cx, float cy, float cz, float cu, float cv,
            float shade)
        {
            // Bounding box clipped to image.
            int minY = Math.Max(0, (int)Math.Floor(Math.Min(Math.Min(ay, by), cy)));
            int maxY = Math.Min(H - 1, (int)Math.Ceiling(Math.Max(Math.Max(ay, by), cy)));
            int minX = Math.Max(0, (int)Math.Floor(Math.Min(Math.Min(ax, bx), cx)));
            int maxX = Math.Min(W - 1, (int)Math.Ceiling(Math.Max(Math.Max(ax, bx), cx)));

            float invArea = EdgeFn(ax, ay, bx, by, cx, cy);
            if (Math.Abs(invArea) < 0.5f) return;
            invArea = 1f / invArea;

            for (int py = minY; py <= maxY; py++)
            {
                for (int px = minX; px <= maxX; px++)
                {
                    float pcx = px + 0.5f, pcy = py + 0.5f;
                    float w0 = EdgeFn(bx, by, cx, cy, pcx, pcy) * invArea;
                    float w1 = EdgeFn(cx, cy, ax, ay, pcx, pcy) * invArea;
                    float w2 = EdgeFn(ax, ay, bx, by, pcx, pcy) * invArea;

                    if (w0 < 0f || w1 < 0f || w2 < 0f) continue;

                    float depth = az * w0 + bz * w1 + cz * w2;
                    int pidx = py * W + px;
                    if (depth < zbuf[pidx]) continue;
                    zbuf[pidx] = depth;

                    float u = au * w0 + bu * w1 + cu * w2;
                    float v = av * w0 + bv * w1 + cv * w2;

                    int tx = (int)(u * sw) % sw;
                    int ty = (int)(v * sh) % sh;
                    if (tx < 0) tx += sw;
                    if (ty < 0) ty += sh;

                    int sp = (ty * sw + tx) * 4;
                    byte sB = skin[sp], sG = skin[sp + 1], sR = skin[sp + 2], sA = skin[sp + 3];
                    if (sA < 128) continue; // transparent skin pixel

                    int op = pidx * 4;
                    outBgra[op]     = (byte)Math.Min(255, (int)(sB * shade));
                    outBgra[op + 1] = (byte)Math.Min(255, (int)(sG * shade));
                    outBgra[op + 2] = (byte)Math.Min(255, (int)(sR * shade));
                    outBgra[op + 3] = 255;
                }
            }
        }

        private static float EdgeFn(float ax, float ay, float bx, float by, float px, float py)
        {
            return (px - ax) * (by - ay) - (py - ay) * (bx - ax);
        }
    }
}
