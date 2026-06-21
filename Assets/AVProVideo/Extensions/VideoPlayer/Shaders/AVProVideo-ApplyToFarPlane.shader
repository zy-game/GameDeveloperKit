Shader "AVProVideo/Background/AVProVideo-ApplyToFarPlane"
{
	Properties
	{
		_MainTex ("Texture", 2D) = "white" {}
		_ChromaTex ("Chroma", 2D) = "gray" {}
		_Color("Main Color", Color) = (1,1,1,1)

		[KeywordEnum(None, Top_Bottom, Left_Right)] AlphaPack("Alpha Pack", Float) = 0
		[KeywordEnum(None, Top_Bottom, Left_Right, Custom_UV)] Stereo("Stereo Mode", Float) = 0
		[Toggle(STEREO_DEBUG)] _StereoDebug("Stereo Debug Tinting", Float) = 0
		[Toggle(APPLY_GAMMA)] _ApplyGamma("Apply Gamma", Float) = 0
		[Toggle(USE_YPCBCR)] _UseYpCbCr("Use YpCbCr", Float) = 0

		_DrawOffset("Draw Offset", Vector) = (0,0,0,0)
		_CustomScale("Custom Scaling", Vector) = (0,0,0,0)
		_Aspect("Aspect Ratio", Float) = 1
		//_TargetCamID("Target Camera", Float) = 0
		//_CurrentCamID("Current Rendering Camera", Float) = 0
	}
	SubShader
	{
		// this is the important part that makes it render behind all of the other object, we set it to be 0 in the queue 
		// Geometry is 2000 and you cant just put a number so Geometry-2000 it is
		Tags { "Queue" = "Geometry-2000" "RenderType"="Opaque" }
		LOD 100
		// then set ZWrite to off so all other items are drawn infront of this one, this is important as the actual object
		// for this is at the near clipping plane of the camera
		ZWrite Off
		Blend SrcAlpha OneMinusSrcAlpha

		Pass
		{
			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag
			#pragma multi_compile_fog

			// TODO: replace use multi_compile_local instead (Unity 2019.1 feature)
			#pragma multi_compile MONOSCOPIC STEREO_TOP_BOTTOM STEREO_LEFT_RIGHT STEREO_CUSTOM_UV
			#pragma multi_compile ALPHAPACK_NONE ALPHAPACK_TOP_BOTTOM ALPHAPACK_LEFT_RIGHT
			#pragma multi_compile __ STEREO_DEBUG
			#pragma multi_compile __ APPLY_GAMMA
			#pragma multi_compile __ USE_YPCBCR

			#include "UnityCG.cginc"
			#include "../../../Runtime/Shaders/AVProVideo.cginc"

			struct appdata
			{
				float4 vertex : POSITION;
				float2 uv : TEXCOORD0;
			#if STEREO_CUSTOM_UV
				float2 uv2 : TEXCOORD1;		// Custom uv set for right eye (left eye is in TEXCOORD0)
			#endif
			#ifdef UNITY_STEREO_INSTANCING_ENABLED
				UNITY_VERTEX_INPUT_INSTANCE_ID
			#endif
			};

			struct v2f
			{
				float4 vertex : SV_POSITION;
				float4 uv : TEXCOORD0;
			
			#if STEREO_DEBUG
				float4 tint : COLOR;
			#endif

				UNITY_FOG_COORDS(1)

			#ifdef UNITY_STEREO_INSTANCING_ENABLED
				UNITY_VERTEX_OUTPUT_STEREO
			#endif
			};

			uniform sampler2D _MainTex;
#if USE_YPCBCR
			uniform sampler2D _ChromaTex; 
			uniform float4x4 _YpCbCrTransform;
#endif
			uniform float4 _MainTex_ST;
			uniform float4 _MainTex_TexelSize;
			uniform float4x4 _MainTex_Xfrm;
			
			uniform float4 _Color;
			uniform float2 _DrawOffset;
			uniform float _Aspect;
			uniform float2 _CustomScale;
			uniform int _TargetCamID;
			uniform int _CurrentCamID;

			v2f vert(appdata v)
			{
				v2f o;

			#ifdef UNITY_STEREO_INSTANCING_ENABLED
				UNITY_SETUP_INSTANCE_ID(v);						// calculates and sets the built-n unity_StereoEyeIndex and unity_InstanceID Unity shader variables to the correct values based on which eye the GPU is currently rendering
				UNITY_INITIALIZE_OUTPUT(v2f, o);				// initializes all v2f values to 0
				UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);		// tells the GPU which eye in the texture array it should render to
			#endif

				// if our position is within 2 unitys of the camera position that is being rendered to
				if (_TargetCamID == _CurrentCamID)
				{
					// scaling
					float height = 1;
					float width = 1;
					// only use AspectRatio scaling if a custom scale has not been set
					if (_CustomScale.x == 0 || _CustomScale.y == 0)
					{
						float2 targetSize = float2(_MainTex_TexelSize.z, _MainTex_TexelSize.w);
						float2 currentSize = float2(_ScreenParams.x / 2, _ScreenParams.y / 2);
						float2 targetAreaSize = float2(_ScreenParams.x, _ScreenParams.y);
						float originalAspectRatio = targetSize.x / targetSize.y;
						float baseTextureAspectRatio = currentSize.x / currentSize.y;
						float targetAspectRatio = baseTextureAspectRatio;
						int finalWidth, finalHeight;

						if (_Aspect == 0) // No Scaling
						{
							// no change wanted here so set the final size to be the size
							// of the orignal image
							finalWidth = (int)targetSize.x;
							finalHeight = (int)targetSize.y;
						}
						else if (_Aspect == 1) // Fit Vertically
						{
							// set the height to that of the target area then mutliply
							// the height by the orignal aspect ratio to ensure that the image
							// stays with the correct aspect.
							finalHeight = (int)targetAreaSize.y;
							finalWidth = round(finalHeight * originalAspectRatio);
						}
						else if (_Aspect == 2) // Fit Horizontally
						{
							// do the same as with FitVertically, just replace the width and heights
							finalWidth = (int)targetAreaSize.x;
							finalHeight = round(finalWidth / originalAspectRatio);
						}
						else if (_Aspect == 3) // Fit Inside
						{
							// if the width is larger then expand to be the same as the target area,
							// cropping the height
							if (targetAspectRatio < originalAspectRatio)
							{
								finalWidth = (int)targetAreaSize.x;
								finalHeight = round(finalWidth / originalAspectRatio);
							}
							// if the height is larger then expand to be the same as the target area,
							// cropping the width
							else
							{
								finalHeight = (int)targetAreaSize.y;
								finalWidth = round(finalHeight * originalAspectRatio);
							}
						}
						else if (_Aspect == 4) // Fit Outside 
						{
							// if the width is smaller, then expand the width to be the same 
							// size as the target then expand the height much like above to ensure
							// that the correct aspect ratio is kept
							if (targetAspectRatio > originalAspectRatio)
							{
								finalWidth = (int)targetAreaSize.x;
								finalHeight = round(finalWidth / originalAspectRatio);
							}
							// if the hight is small, expand that first then make the width follow
							else
							{
								finalHeight = (int)targetAreaSize.y;
								finalWidth = round(finalHeight * originalAspectRatio);
							}
						}
						else if (_Aspect == 5) // Stretch
						{
							// set the width and the height to be the same size as the target area
							finalWidth = (int)targetAreaSize.x;
							finalHeight = (int)targetAreaSize.y;
						}
						else // No Scalling
						{
							// make no change keeping them as the orignal texture size (1/4) of the screen
							finalWidth = (int)currentSize.x;
							finalHeight = (int)currentSize.y;
						}

						height = (float)finalHeight / (float)_ScreenParams.y;
						width = (float)finalWidth / (float)_ScreenParams.x;
					}
					else
					{
						// use custom scaling
						width = _CustomScale.x / (float)_ScreenParams.x;
						height = _CustomScale.y / (float)_ScreenParams.y;
					}
					float2 pos = (v.vertex.xy - float2(0.5, 0.5) + _DrawOffset.xy) * 2.0;
					pos.x *= width;
					pos.y *= height;

					// flip if needed then done
					if (_ProjectionParams.x < 0.0)
					{
						pos.y = (1.0 - pos.y) - 1.0;
					}
					o.vertex = float4(pos.xy, UNITY_NEAR_CLIP_VALUE, 1.0);
				}
				else
				{
					o.vertex = UnityObjectToClipPos(float4(0,0,0,0));
				}

				// Apply texture transformation matrix - adjusts for offset/cropping (when the decoder decodes in blocks that overrun the video frame size, it pads)
				o.uv.xy = mul(_MainTex_Xfrm, float4(v.uv.xy, 0.0, 1.0)).xy;
				o.uv.xy = TRANSFORM_TEX(o.uv.xy, _MainTex);

				// Horrible hack to undo the scale transform to fit into our UV packing layout logic...
				if (_MainTex_ST.y < 0.0)
				{
					o.uv.y = 1.0 - o.uv.y;
				}

			#if STEREO_TOP_BOTTOM | STEREO_LEFT_RIGHT
				float4 scaleOffset = GetStereoScaleOffset(IsStereoEyeLeft(), _MainTex_ST.y < 0.0);
				o.uv.xy *= scaleOffset.xy;
				o.uv.xy += scaleOffset.zw;
			#elif STEREO_CUSTOM_UV
				if (!IsStereoEyeLeft())
				{
					o.uv.xy = TRANSFORM_TEX(v.uv2, _MainTex);
				}
			#endif

			#if STEREO_DEBUG
				o.tint = GetStereoDebugTint(IsStereoEyeLeft());
			#endif

				o.uv = OffsetAlphaPackingUV(_MainTex_TexelSize.xy, o.uv.xy, _MainTex_ST.y < 0.0);

				UNITY_TRANSFER_FOG(o, o.vertex);

				return o;
			}
			
			fixed4 frag (v2f i) : SV_Target
			{
				float4 col;
			#if USE_YPCBCR
				col = SampleYpCbCr(_MainTex, _ChromaTex, i.uv.xy, _YpCbCrTransform);
			#else
				col = SampleRGBA(_MainTex, i.uv.xy);
			#endif

			#if ALPHAPACK_TOP_BOTTOM | ALPHAPACK_LEFT_RIGHT
				col.a = SamplePackedAlpha(_MainTex, i.uv.zw);
			#endif

				col *= _Color;

			#if STEREO_DEBUG
				col *= i.tint;
			#endif

				UNITY_APPLY_FOG(i.fogCoord, col);

				return col;
			}
			ENDCG
		}
	}
}
