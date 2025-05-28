Shader "Custom RP/ComputeVisualize"
{
    Properties
    {

    }
    SubShader
    {
		HLSLPROGRAM
		#pragma enable_d3d11_debug_symbols
		ENDHLSL
        Pass
        {
            Name "ComputeVisualizePass"
            Tags
            {
                "LightMode" = "ComputeVisualize"
			}
            HLSLPROGRAM

            #pragma vertex ComputeVisualizeVertex
            #pragma fragment ComputeVisualizeFragment
            #pragma multi_compile_instancing

            #include "ComputeVisualizePass.hlsl"

            ENDHLSL
        }
    }
}
