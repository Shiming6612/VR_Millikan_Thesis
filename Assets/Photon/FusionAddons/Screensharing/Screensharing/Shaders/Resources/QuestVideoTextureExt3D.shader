Shader "PhotonVoiceApi/GLES3/QuestVideoTextureExt3D" {
    Properties{
        _MainTex("Texture", 2D) = "red" {}
        _Flip("Flip", Vector) = (1, 1, 0, 0)
    }

        GLSLINCLUDE
#include "UnityCG.glslinc"
            ENDGLSL

            // https://stackoverflow.com/questions/25618977/how-to-render-to-a-gl-texture-external-oes
            // https://github.com/TwoTailsGames/Unity-Built-in-Shaders/blob/master/DefaultResourcesExtra/VideoDecodeAndroid.shader
            SubShader{
                Pass {
                    GLSLPROGRAM
                    #if defined(STEREO_MULTIVIEW_ON)
                        // Helper methods for stereo rendering https://unity3d.com/unity/whats-new/2018.3.14
                        UNITY_SETUP_STEREO_RENDERING
                    #endif

                    #ifdef VERTEX

                    // URP Compatibility:
                    // unity_ObjectToWorld  won't work with URP by default (https://docs.unity3d.com/Packages/com.unity.render-pipelines.universal@15.0/manual/shaders-in-universalrp.html)
                    // CBUFFER sections are available in HLSL only, so we're passing the matrix manually here.
                    // Need to be filled during Update() with material.SetMatrix("_localToWorldMatrix", renderer.transform.localToWorldMatrix);
                    uniform mat4 _localToWorldMatrix;

                    uniform vec4 _Flip;

                    varying vec2 vTextureCoord;
                    void main() {
                        int eye = SetupStereoEyeIndex();
                        gl_Position = GetStereoMatrixVP(eye) * _localToWorldMatrix * gl_Vertex;
                        vTextureCoord = (gl_MultiTexCoord0.st - vec2(0.5, 0.5)) * _Flip.xy * vec2(1, -1) + vec2(0.5, 0.5);
                    }

                    #endif

                    #ifdef FRAGMENT

                    // SHADER_API_GLES3 does not work here
                    #extension GL_OES_EGL_image_external_essl3 : require

                    precision mediump float;
                    uniform samplerExternalOES _MainTex;
                    varying vec2 vTextureCoord;
                    void main() {
                        #ifdef SHADER_API_GLES3
                        // Force transparency to prevent issues when HDR is not enabled. 
                        vec4 color = texture(_MainTex, vTextureCoord);
                        gl_FragColor = vec4(color.rgb, 1.0);
                        //With HDR enabled, you can use instead:
                        //gl_FragColor = texture(_MainTex, vTextureCoord);
                        #else
                        gl_FragColor = textureExternal(_MainTex, vTextureCoord);
                        #endif
                        //gl_FragColor = vec4(vTextureCoord.x, vTextureCoord.y, 0, 1);
                    }
                    #endif

                    ENDGLSL
                }
        }
}