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

Shader "Proland/Terrain/OriginalUpSampleElevation" 
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
			
			// tile border size
			#define BORDER 2.0 
			
			//x - size in pixels of one tile (including borders), 
			//y - size in meters of a pixel of the elevation texture, 
			//z - (tileWidth - 2*BORDER) / grid mesh size for display, 
			uniform float4 _TileWSD;
			// coarse level texture
			uniform sampler2D _CoarseLevelSampler; 
			// lower left corner of patch to upsample, one over size in pixels of coarse level texture, layer id
			uniform float4 _CoarseLevelOSL; 
			// noise texture
			uniform sampler2D _NoiseSampler; 
			// noise texture rotation, noise texture layer, scaling factor of noise texture values
			uniform float4 _NoiseUVLH; 
			
			uniform sampler2D _ResidualSampler;
			uniform float4 _ResidualOSH;
		
			static float4x4 slopexMatrix[4] = {
			{0.0, 0.0, 0.0, 0.0,
	         1.0, 0.0, -1.0, 0.0,
	         0.0, 0.0, 0.0, 0.0,
	         0.0, 0.0, 0.0, 0.0},
	    	{0.0, 0.0, 0.0, 0.0,
	         0.5, 0.5, -0.5, -0.5,
	         0.0, 0.0, 0.0, 0.0,
	         0.0, 0.0, 0.0, 0.0},
	    	{0.0, 0.0, 0.0, 0.0,
	         0.5, 0.0, -0.5, 0.0,
	         0.5, 0.0, -0.5, 0.0,
	         0.0, 0.0, 0.0, 0.0},
	    	{0.0, 0.0, 0.0, 0.0,
	         0.25, 0.25, -0.25, -0.25,
	         0.25, 0.25, -0.25, -0.25,
	         0.0, 0.0, 0.0, 0.0}};
	         
	        static float4x4 slopeyMatrix[4] = {
			{0.0, 1.0, 0.0, 0.0,
			 0.0, 0.0, 0.0, 0.0,
			 0.0, -1.0, 0.0, 0.0,
			 0.0, 0.0, 0.0, 0.0},
			{0.0, 0.5, 0.5, 0.0,
			 0.0, 0.0, 0.0, 0.0,
			 0.0, -0.5, -0.5, 0.0,
			 0.0, 0.0, 0.0, 0.0},
			{0.0, 0.5, 0.0, 0.0,
			 0.0, 0.5, 0.0, 0.0,
			 0.0, -0.5, 0.0, 0.0,
			 0.0, -0.5, 0.0, 0.0},
			{0.0, 0.25, 0.25, 0.0,
			 0.0, 0.25, 0.25, 0.0,
			 0.0, -0.25, -0.25, 0.0,
			 0.0, -0.25, -0.25, 0.0}};
			 
			static float4x4 curvatureMatrix[4] = {
			{0.0, -1.0, 0.0, 0.0,
			 -1.0, 4.0, -1.0, 0.0,
			 0.0, -1.0, 0.0, 0.0,
			 0.0, 0.0, 0.0, 0.0},
			{0.0, -0.5, -0.5, 0.0,
			 -0.5, 1.5, 1.5, -0.5,
			 0.0, -0.5, -0.5, 0.0,
			 0.0, 0.0, 0.0, 0.0},
			{0.0, -0.5, 0.0, 0.0,
			 -0.5, 1.5, -0.5, 0.0,
			 -0.5, 1.5, -0.5, 0.0,
			 0.0, -0.5, 0.0, 0.0},
			{0.0, -0.25, -0.25, 0.0,
			 -0.25, 0.5, 0.5, -0.25,
			 -0.25, 0.5, 0.5, -0.25,
			 0.0, -0.25, -0.25, 0.0}};
			 
			static float4x4 upsampleMatrix[4] = {
			{0.0, 0.0, 0.0, 0.0,
			 0.0, 1.0, 0.0, 0.0,
			 0.0, 0.0, 0.0, 0.0,
			 0.0, 0.0, 0.0, 0.0},
			{0.0, 0.0, 0.0, 0.0,
			 -1.0/16.0, 9.0/16.0, 9.0/16.0, -1.0/16.0,
			 0.0, 0.0, 0.0, 0.0,
			 0.0, 0.0, 0.0, 0.0},
			{0.0, -1.0/16.0, 0.0, 0.0,
			 0.0, 9.0/16.0, 0.0, 0.0,
			 0.0, 9.0/16.0, 0.0, 0.0,
			 0.0, -1.0/16.0, 0.0, 0.0},
			{1.0/256.0, -9.0/256.0, -9.0/256.0, 1.0/256.0,
			 -9.0/256.0, 81.0/256.0, 81.0/256.0, -9.0/256.0,
			 -9.0/256.0, 81.0/256.0, 81.0/256.0, -9.0/256.0,
			 1.0/256.0, -9.0/256.0, -9.0/256.0, 1.0/256.0}};
			 
			struct v2f 
			{
    			float4  pos : SV_POSITION;
    			float2  uv : TEXCOORD0;
    			float2  st : TEXCOORD1;
			};

			v2f vert(appdata_base v)
			{
    			v2f OUT;
    			OUT.pos = UnityObjectToClipPos(v.vertex);
    			OUT.uv = v.texcoord.xy;
    			OUT.st = v.texcoord.xy * _TileWSD.x;
    			return OUT;
			}
			
			float mdot(float4x4 a, float4x4 b) {
			    return dot(a[0], b[0]) + dot(a[1], b[1]) + dot(a[2], b[2]) + dot(a[3], b[3]);
			}
			
			float4 frag(v2f IN) : COLOR
			{
			
				float2 p_uv = floor(IN.st) * 0.5;
				float2 uv = (p_uv - frac(p_uv) + float2(0.5,0.5)) * _CoarseLevelOSL.z + _CoarseLevelOSL.xy;
				
				float2 residual_uv = p_uv * _ResidualOSH.z + _ResidualOSH.xy;
    			float zf = _ResidualOSH.w * tex2Dlod(_ResidualSampler, float4(residual_uv, 0, 0)).x;
				
				float4x4 cz = {
			        tex2Dlod(_CoarseLevelSampler, float4(uv + float2(0.0, 0.0) *  _CoarseLevelOSL.z, 0.0, 0.0)).x,
			        tex2Dlod(_CoarseLevelSampler, float4(uv + float2(1.0, 0.0) *  _CoarseLevelOSL.z, 0.0, 0.0)).x,
			        tex2Dlod(_CoarseLevelSampler, float4(uv + float2(2.0, 0.0) *  _CoarseLevelOSL.z, 0.0, 0.0)).x,
			        tex2Dlod(_CoarseLevelSampler, float4(uv + float2(3.0, 0.0) *  _CoarseLevelOSL.z, 0.0, 0.0)).x,
			        tex2Dlod(_CoarseLevelSampler, float4(uv + float2(0.0, 1.0) *  _CoarseLevelOSL.z, 0.0, 0.0)).x,
			        tex2Dlod(_CoarseLevelSampler, float4(uv + float2(1.0, 1.0) *  _CoarseLevelOSL.z, 0.0, 0.0)).x,
			        tex2Dlod(_CoarseLevelSampler, float4(uv + float2(2.0, 1.0) *  _CoarseLevelOSL.z, 0.0, 0.0)).x,
			        tex2Dlod(_CoarseLevelSampler, float4(uv + float2(3.0, 1.0) *  _CoarseLevelOSL.z, 0.0, 0.0)).x,
			        tex2Dlod(_CoarseLevelSampler, float4(uv + float2(0.0, 2.0) *  _CoarseLevelOSL.z, 0.0, 0.0)).x,
			        tex2Dlod(_CoarseLevelSampler, float4(uv + float2(1.0, 2.0) *  _CoarseLevelOSL.z, 0.0, 0.0)).x,
			        tex2Dlod(_CoarseLevelSampler, float4(uv + float2(2.0, 2.0) *  _CoarseLevelOSL.z, 0.0, 0.0)).x,
			        tex2Dlod(_CoarseLevelSampler, float4(uv + float2(3.0, 2.0) *  _CoarseLevelOSL.z, 0.0, 0.0)).x,
			        tex2Dlod(_CoarseLevelSampler, float4(uv + float2(0.0, 3.0) *  _CoarseLevelOSL.z, 0.0, 0.0)).x,
			        tex2Dlod(_CoarseLevelSampler, float4(uv + float2(1.0, 3.0) *  _CoarseLevelOSL.z, 0.0, 0.0)).x,
			        tex2Dlod(_CoarseLevelSampler, float4(uv + float2(2.0, 3.0) *  _CoarseLevelOSL.z, 0.0, 0.0)).x,
			        tex2Dlod(_CoarseLevelSampler, float4(uv + float2(3.0, 3.0) *  _CoarseLevelOSL.z, 0.0, 0.0)).x
			    };
			    
			    int i = int(dot(frac(p_uv), float2(2.0, 4.0)));
			    float3 n = float3(mdot(cz, slopexMatrix[i]), mdot(cz, slopeyMatrix[i]), 2.0 * _TileWSD.y);
			    float slope = length(n.xy) / n.z;
			    float curvature = mdot(cz, curvatureMatrix[i]) / _TileWSD.y;
			    float noiseAmp = max(clamp(4.0 * curvature, 0.0, 1.5), clamp(2.0 * slope - 0.5, 0.1, 4.0));
			    
			    float2 nuv = (floor(IN.st) + float2(0.5,0.5)) / _TileWSD.x;
			    float4 uvs = float4(nuv, float2(1.0,1.0) - nuv);
			    
			    float2 noiseUV = float2(uvs[int(_NoiseUVLH.x)], uvs[int(_NoiseUVLH.y)]);
			    float noise = tex2Dlod(_NoiseSampler, float4(noiseUV, 0.0, 0.0)).x;
			    
			    noise += _NoiseUVLH.z;
			    
			    if (_NoiseUVLH.w < 0.0) {
			        zf -= _NoiseUVLH.w * noise;
			    }
			    else {
			        zf += noiseAmp * _NoiseUVLH.w * noise;
			    }
			    
			    float zc = zf;
			    if (_CoarseLevelOSL.x != -1.0) 
			    {
			        zf = zf + mdot(cz, upsampleMatrix[i]);

			        float2 ij = floor(IN.st - float2(BORDER,BORDER));
			        float4 uvc = float4(BORDER + 0.5,BORDER + 0.5,BORDER + 0.5,BORDER + 0.5);
			        uvc += _TileWSD.z * floor((ij / (2.0 * _TileWSD.z)).xyxy + float4(0.5, 0.0, 0.0, 0.5));
			        
			        float zc1 = tex2Dlod(_CoarseLevelSampler, float4(uvc.xy * _CoarseLevelOSL.z + _CoarseLevelOSL.xy, 0.0, 0.0)).x;
			        float zc3 = tex2Dlod(_CoarseLevelSampler, float4(uvc.zw * _CoarseLevelOSL.z + _CoarseLevelOSL.xy, 0.0, 0.0)).x;
			        
			        zc = (zc1 + zc3) * 0.5;
			    }
			    
				return float4(zf, zc, 0.0, 0.0);
			    
			}
			
			ENDCG

    	}
	}
}
























