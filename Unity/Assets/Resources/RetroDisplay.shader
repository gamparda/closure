Shader "Closure/Retro Display"
{
 Properties { _MainTex ("Low-resolution world", 2D) = "white" {} }
 SubShader {
  Tags { "Queue"="Overlay" "RenderType"="Transparent" }
  Cull Off ZWrite Off ZTest Always
  Pass {
   CGPROGRAM
   #pragma vertex vert
   #pragma fragment frag
   #include "UnityCG.cginc"
   struct appdata { float4 vertex:POSITION; float2 uv:TEXCOORD0; };
   struct v2f { float4 vertex:SV_POSITION; float2 uv:TEXCOORD0; };
   sampler2D _MainTex; float4 _MainTex_TexelSize; float _ClosureDrift;
   v2f vert(appdata v) { v2f o; o.vertex=UnityObjectToClipPos(v.vertex);o.uv=v.uv;return o; }
   fixed4 frag(v2f i):SV_Target {
    float2 uv=i.uv;uv.x+=sin(uv.y*140+_Time.y*3)*.00045*_ClosureDrift;
    float3 c=tex2D(_MainTex,uv).rgb;
    float n=frac(sin(dot(floor(uv*_MainTex_TexelSize.zw)+floor(_Time.y*18),float2(12.9898,78.233)))*43758.5453);
    c+=(n-.5)*(.02+_ClosureDrift*.05);
    float d=fmod(floor(uv.x*_MainTex_TexelSize.z)+fmod(floor(uv.y*_MainTex_TexelSize.w),2)*2,4);
    c=floor(c*32+d*.17)/32;
    float vignette=saturate(1-length((uv-.5)*1.3));
    return fixed4(c*(.7+.3*vignette),1);
   }
   ENDCG
  }
 }
}
