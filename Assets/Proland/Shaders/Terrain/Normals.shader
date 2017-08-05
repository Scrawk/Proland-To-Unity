// Upgrade NOTE: replaced 'mul(UNITY_MATRIX_MVP,*)' with 'UnityObjectToClipPos(*)'


/*
 * Proland: a procedural landscape rendering library.
 * Copyright (c) 2008-2011 INRIA
 *
 * This program is free software: you can redistribute it and/or modify
 * it under the terms of the GNU General Public License as published by
 * the Free Software Foundation, either version 3 of the License, or
 * (at your option) any later version.
 *
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 * GNU General Public License for more details.
 *
 * You should have received a copy of the GNU General Public License
 * along with this program.  If not, see <http://www.gnu.org/licenses/>.
 */

/*
 * Proland is distributed under a dual-license scheme.
 * You can obtain a specific license from Inria: proland-licensing@inria.fr.
 */

/*
 * Authors: Eric Bruneton, Antoine Begault, Guillaume Piolat.
 */

Shader "Proland/Terrain/Normals" 
{
	SubShader 
	{
    	Pass 
    	{
			ZTest Always Cull Off ZWrite Off
      		Fog { Mode off }

			CGPROGRAM
			#include "UnityCG.cginc"
			#pragma target 4.0
			#pragma vertex vert
			#pragma fragment frag
			
			// size in pixels of one tile, tileSize / grid mesh size for display, output format
			uniform float3 _TileSD;
			// elevation texture
			uniform sampler2D _ElevationSampler; 
			// lower left corner of tile containing patch elevation, one over size in pixels of tiles texture, layer id
			uniform float4 _ElevationOSL; 
			
			uniform float4x4 _PatchCorners;
			uniform float4x4 _PatchVerticals;
			uniform float4 _PatchCornerNorms;
			uniform float4x4 _WorldToTangentFrame;
			uniform float4 _Deform;
		
			struct v2f 
			{
    			float4  pos : SV_POSITION;
    			float2  uv : TEXCOORD0;
    			float2 st : TEXCOORD1;
			};

			v2f vert(appdata_base v)
			{
    			v2f OUT;
    			OUT.pos = UnityObjectToClipPos(v.vertex);
    			OUT.uv = v.texcoord.xy;
    			OUT.st = v.texcoord.xy * _TileSD.x;
    			return OUT;
			}
			
			float3 GetWorldPosition(float2 uv, float h) 
			{
			    float3 p;
			    uv = uv / (_TileSD.x - 1.0);
			    
			    if (_Deform.w == 0.0) 
			    {
			        p = float3(_Deform.xy + _Deform.z * uv, h);
			    } 
			    else 
			    {
			        float R = _Deform.w;
			        float4x4 C = _PatchCorners;
			        float4x4 N = _PatchVerticals;
			        float4 L = _PatchCornerNorms;

			        float4 uvUV = float4(uv, float2(1.0,1.0) - uv);
			        float4 alpha = uvUV.zxzx * uvUV.wwyy;
			        float4 alphaPrime = alpha * L / dot(alpha, L);

			        float4 up = mul(N, alphaPrime);
			        float k = lerp(length(up.xyz), 1.0, smoothstep(R / 32.0, R / 64.0, _Deform.z));
			        float hPrime = (h + R * (1.0 - k)) / k;

			        p = (mul(C, alphaPrime) + hPrime * up).xyz;
			    }
			    return p;
			}
			
			float4 frag(v2f IN) : COLOR
			{
			    float2 p_uv = floor(IN.st);

			    float4 uv0 = floor(p_uv.xyxy + float4(-1.0,0.0,1.0,0.0)) * _ElevationOSL.z + _ElevationOSL.xyxy;
			    float4 uv1 = floor(p_uv.xyxy + float4(0.0,-1.0,0.0,1.0)) * _ElevationOSL.z + _ElevationOSL.xyxy;
			    
			    float z0 = tex2Dlod(_ElevationSampler, float4(uv0.xy, 0.0, 0.0)).x;
			    float z1 = tex2Dlod(_ElevationSampler, float4(uv0.zw, 0.0, 0.0)).x;
			    float z2 = tex2Dlod(_ElevationSampler, float4(uv1.xy, 0.0, 0.0)).x;
			    float z3 = tex2Dlod(_ElevationSampler, float4(uv1.zw, 0.0, 0.0)).x;

			    float3 p0 = GetWorldPosition(p_uv + float2(-1.0, 0.0), z0).xyz;
			    float3 p1 = GetWorldPosition(p_uv + float2(+1.0, 0.0), z1).xyz;
			    float3 p2 = GetWorldPosition(p_uv + float2(0.0, -1.0), z2).xyz;
			    float3 p3 = GetWorldPosition(p_uv + float2(0.0, +1.0), z3).xyz;
			    
			    float3x3 worldToTangentFrame = _WorldToTangentFrame;
			    
			    float2 nf = (mul(worldToTangentFrame, normalize(cross(p1 - p0, p3 - p2)))).xy;
			    
				return float4(nf,0,0);
			}
			
			ENDCG

    	}
	}
}
















