using UnityEngine.Experimental.Rendering;
using UnityEngine.Experimental.Rendering.RenderGraphModule;

namespace UnityEngine.Rendering.HighDefinition
{
    public partial class HDRenderPipeline
    {
        // Intermediate textures required for baking the clouds into the cubemap
        RTHandle m_IntermediateCloudsLightingBuffer = null;
        RTHandle m_IntermediateCloudsDepthBuffer = null;
        RTHandle m_IntermediateCloudsLightingCube0Buffer = null;
        RTHandle m_IntermediateCloudsLightingCube1Buffer = null;
        MaterialPropertyBlock m_MpbClouds = new MaterialPropertyBlock();

        void ReleaseVolumetricCloudsStaticTextures()
        {
            if (m_Asset.currentPlatformRenderPipelineSettings.supportVolumetricClouds)
            {
                RTHandles.Release(m_IntermediateCloudsLightingBuffer);
                RTHandles.Release(m_IntermediateCloudsDepthBuffer);
                RTHandles.Release(m_IntermediateCloudsLightingCube0Buffer);
                RTHandles.Release(m_IntermediateCloudsLightingCube1Buffer);
            }
        }

        void InitializeVolumetricCloudsStaticTextures()
        {
            if (m_Asset.currentPlatformRenderPipelineSettings.supportVolumetricClouds)
            {
                int skyResolution = (int)m_Asset.currentPlatformRenderPipelineSettings.lightLoopSettings.skyReflectionSize;
                m_IntermediateCloudsLightingBuffer = RTHandles.Alloc(skyResolution, skyResolution, TextureXR.slices, dimension: TextureDimension.Tex2DArray, colorFormat: GraphicsFormat.R16G16B16A16_SFloat, enableRandomWrite: true);
                m_IntermediateCloudsDepthBuffer = RTHandles.Alloc(skyResolution, skyResolution, TextureXR.slices, dimension: TextureDimension.Tex2DArray, colorFormat: GraphicsFormat.R32_SFloat, enableRandomWrite: true);
                m_IntermediateCloudsLightingCube0Buffer = RTHandles.Alloc(skyResolution, skyResolution, TextureXR.slices, dimension: TextureDimension.Cube, colorFormat: GraphicsFormat.R16G16B16A16_SFloat, enableRandomWrite: true, useMipMap: true, autoGenerateMips: false);
                m_IntermediateCloudsLightingCube1Buffer = RTHandles.Alloc(skyResolution, skyResolution, TextureXR.slices, dimension: TextureDimension.Cube, colorFormat: GraphicsFormat.R16G16B16A16_SFloat, enableRandomWrite: true, useMipMap: true, autoGenerateMips: false);
            }
        }

        struct VolumetricCloudsParameters_Sky_Low
        {
            // Resolution parameters
            public int traceWidth;
            public int traceHeight;
            public int intermediateWidth;
            public int intermediateHeight;
            public int finalWidth;
            public int finalHeight;

            // Used kernels
            public int renderKernel;
            public int preUpscaleKernel;
            public int finalUpscaleKernel;

            // Data common to all volumetric cloud passes
            public VolumetricCloudCommonData commonData;

            // Sky
            public Material cloudCombinePass;
            public CubemapFace cubemapFace;
        }

        VolumetricCloudsParameters_Sky_Low PrepareVolumetricCloudsParameters_Sky_Low(HDCamera hdCamera, int width, int height, CubemapFace cubemapFace, VolumetricClouds settings)
        {
            VolumetricCloudsParameters_Sky_Low parameters = new VolumetricCloudsParameters_Sky_Low();

            // Compute the cloud model data
            CloudModelData cloudModelData = GetCloudModelData(settings);

            // Fill the common data
            FillVolumetricCloudsCommonData(false, settings, TVolumetricCloudsCameraType.Sky, in cloudModelData, ref parameters.commonData);

            // We need to make sure that the allocated size of the history buffers and the dispatch size are perfectly equal.
            // The ideal approach would be to have a function for that returns the converted size from a viewport and texture size.
            // but for now we do it like this.
            // Final resolution at which the effect should be exported
            parameters.finalWidth = width;
            parameters.finalHeight = height;
            // Intermediate resolution at which the effect is accumulated
            parameters.intermediateWidth = Mathf.RoundToInt(0.5f * width);
            parameters.intermediateHeight = Mathf.RoundToInt(0.5f * height);
            // Resolution at which the effect is traced
            parameters.traceWidth = Mathf.RoundToInt(0.25f * width);
            parameters.traceHeight = Mathf.RoundToInt(0.25f * height);

            // Sky
            parameters.cubemapFace = cubemapFace;
            parameters.cloudCombinePass = m_CloudCombinePass;

            // Kernels
            parameters.renderKernel = m_CloudRenderKernel;
            parameters.preUpscaleKernel = m_PreUpscaleCloudsSkyKernel;
            parameters.finalUpscaleKernel = m_UpscaleAndCombineCloudsSkyKernel;


            // Update the constant buffer
            VolumetricCloudsCameraData cameraData;
            cameraData.cameraType = parameters.commonData.cameraType;
            cameraData.traceWidth = parameters.traceWidth;
            cameraData.traceHeight = parameters.traceHeight;
            cameraData.intermediateWidth = parameters.intermediateWidth;
            cameraData.intermediateHeight = parameters.intermediateHeight;
            cameraData.finalWidth = parameters.finalWidth;
            cameraData.finalHeight = parameters.finalHeight;
            cameraData.viewCount = 1;
            cameraData.enableExposureControl = parameters.commonData.enableExposureControl;
            cameraData.lowResolution = true;
            cameraData.enableIntegration = false;
            UpdateShaderVariableslClouds(ref parameters.commonData.cloudsCB, hdCamera, settings, cameraData, cloudModelData, false);

            return parameters;
        }

        static void TraceVolumetricClouds_Sky_Low(CommandBuffer cmd, in VolumetricCloudsParameters_Sky_Low parameters, MaterialPropertyBlock mpb, RTHandle intermediateLightingBuffer0, RTHandle intermediateDepthBuffer0, RTHandle intermediateCubeMap)
        {
            // Compute the number of tiles to evaluate
            int traceTX = (parameters.traceWidth + (8 - 1)) / 8;
            int traceTY = (parameters.traceHeight + (8 - 1)) / 8;

            // Bind the sampling textures
            BlueNoise.BindDitheredTextureSet(cmd, parameters.commonData.ditheredTextureSet);

            // Bind the constant buffer
            ConstantBuffer.Push(cmd, parameters.commonData.cloudsCB, parameters.commonData.volumetricCloudsCS, HDShaderIDs._ShaderVariablesClouds);

            CoreUtils.SetKeyword(cmd, "LOCAL_VOLUMETRIC_CLOUDS", false);

            // Ray-march the clouds for this frame
            CoreUtils.SetKeyword(cmd, "PHYSICALLY_BASED_SUN", parameters.commonData.cloudsCB._PhysicallyBasedSun == 1);
            cmd.SetComputeTextureParam(parameters.commonData.volumetricCloudsCS, parameters.renderKernel, HDShaderIDs._Worley128RGBA, parameters.commonData.worley128RGBA);
            cmd.SetComputeTextureParam(parameters.commonData.volumetricCloudsCS, parameters.renderKernel, HDShaderIDs._ErosionNoise, parameters.commonData.erosionNoise);
            cmd.SetComputeTextureParam(parameters.commonData.volumetricCloudsCS, parameters.renderKernel, HDShaderIDs._CloudMapTexture, parameters.commonData.cloudMapTexture);
            cmd.SetComputeTextureParam(parameters.commonData.volumetricCloudsCS, parameters.renderKernel, HDShaderIDs._CloudLutTexture, parameters.commonData.cloudLutTexture);
            cmd.SetComputeTextureParam(parameters.commonData.volumetricCloudsCS, parameters.renderKernel, HDShaderIDs._CloudsLightingTextureRW, intermediateLightingBuffer0);
            cmd.SetComputeTextureParam(parameters.commonData.volumetricCloudsCS, parameters.renderKernel, HDShaderIDs._CloudsDepthTextureRW, intermediateDepthBuffer0);
            cmd.DispatchCompute(parameters.commonData.volumetricCloudsCS, parameters.renderKernel, traceTX, traceTY, 1);
            CoreUtils.SetKeyword(cmd, "PHYSICALLY_BASED_SUN", false);

            mpb.SetTexture(HDShaderIDs._VolumetricCloudsUpscaleTextureRW, intermediateLightingBuffer0);
            CoreUtils.SetRenderTarget(cmd, intermediateCubeMap, ClearFlag.None, miplevel: 2, cubemapFace: parameters.cubemapFace);
            CoreUtils.DrawFullScreen(cmd, parameters.cloudCombinePass, mpb, 2);
        }

        static void TraceVolumetricClouds_Sky_Low(CommandBuffer cmd, VolumetricCloudsParameters_Sky_Low parameters,
            RTHandle colorBuffer, RTHandle intermediateLightingBuffer0, RTHandle intermediateLightingBuffer1, RTHandle intermediateDepthBuffer0, RTHandle intermediateUpscaleBuffer)
        {
            // Compute the number of tiles to evaluate
            int traceTX = (parameters.traceWidth + (8 - 1)) / 8;
            int traceTY = (parameters.traceHeight + (8 - 1)) / 8;

            // Compute the number of tiles to evaluate
            int intermediateTX = (parameters.intermediateWidth + (8 - 1)) / 8;
            int intermediateTY = (parameters.intermediateHeight + (8 - 1)) / 8;

            // Compute the number of tiles to evaluate
            int finalTX = (parameters.finalWidth + (8 - 1)) / 8;
            int finalTY = (parameters.finalHeight + (8 - 1)) / 8;

            // Bind the sampling textures
            BlueNoise.BindDitheredTextureSet(cmd, parameters.commonData.ditheredTextureSet);

            // Set the multi compiles
            CoreUtils.SetKeyword(cmd, "LOCAL_VOLUMETRIC_CLOUDS", false);

            // Bind the constant buffer
            ConstantBuffer.Push(cmd, parameters.commonData.cloudsCB, parameters.commonData.volumetricCloudsCS, HDShaderIDs._ShaderVariablesClouds);

            using (new ProfilingScope(cmd, ProfilingSampler.Get(HDProfileId.VolumetricCloudsTrace)))
            {
                // Ray-march the clouds for this frame
                CoreUtils.SetKeyword(cmd, "PHYSICALLY_BASED_SUN", parameters.commonData.cloudsCB._PhysicallyBasedSun == 1);
                cmd.SetComputeTextureParam(parameters.commonData.volumetricCloudsCS, parameters.renderKernel, HDShaderIDs._Worley128RGBA, parameters.commonData.worley128RGBA);
                cmd.SetComputeTextureParam(parameters.commonData.volumetricCloudsCS, parameters.renderKernel, HDShaderIDs._ErosionNoise, parameters.commonData.erosionNoise);
                cmd.SetComputeTextureParam(parameters.commonData.volumetricCloudsCS, parameters.renderKernel, HDShaderIDs._CloudMapTexture, parameters.commonData.cloudMapTexture);
                cmd.SetComputeTextureParam(parameters.commonData.volumetricCloudsCS, parameters.renderKernel, HDShaderIDs._CloudLutTexture, parameters.commonData.cloudLutTexture);
                cmd.SetComputeTextureParam(parameters.commonData.volumetricCloudsCS, parameters.renderKernel, HDShaderIDs._CloudsLightingTextureRW, intermediateLightingBuffer0);
                cmd.SetComputeTextureParam(parameters.commonData.volumetricCloudsCS, parameters.renderKernel, HDShaderIDs._CloudsDepthTextureRW, intermediateDepthBuffer0);
                cmd.DispatchCompute(parameters.commonData.volumetricCloudsCS, parameters.renderKernel, traceTX, traceTY, 1);
                CoreUtils.SetKeyword(cmd, "PHYSICALLY_BASED_SUN", false);
            }

            using (new ProfilingScope(cmd, ProfilingSampler.Get(HDProfileId.VolumetricCloudsPreUpscale)))
            {
                cmd.SetComputeTextureParam(parameters.commonData.volumetricCloudsCS, parameters.preUpscaleKernel, HDShaderIDs._CloudsLightingTexture, intermediateLightingBuffer0);
                cmd.SetComputeTextureParam(parameters.commonData.volumetricCloudsCS, parameters.preUpscaleKernel, HDShaderIDs._CloudsLightingTextureRW, intermediateLightingBuffer1);
                cmd.DispatchCompute(parameters.commonData.volumetricCloudsCS, parameters.preUpscaleKernel, intermediateTX, intermediateTY, 1);
            }

            using (new ProfilingScope(cmd, ProfilingSampler.Get(HDProfileId.VolumetricCloudsUpscaleAndCombine)))
            {
                cmd.SetComputeTextureParam(parameters.commonData.volumetricCloudsCS, parameters.finalUpscaleKernel, HDShaderIDs._VolumetricCloudsTexture, intermediateLightingBuffer1);
                cmd.SetComputeTextureParam(parameters.commonData.volumetricCloudsCS, parameters.finalUpscaleKernel, HDShaderIDs._VolumetricCloudsUpscaleTextureRW, intermediateUpscaleBuffer);
                cmd.DispatchCompute(parameters.commonData.volumetricCloudsCS, parameters.finalUpscaleKernel, finalTX, finalTY, 1);

                parameters.cloudCombinePass.SetTexture(HDShaderIDs._VolumetricCloudsUpscaleTextureRW, intermediateUpscaleBuffer);
                CoreUtils.SetRenderTarget(cmd, colorBuffer, ClearFlag.None, 0, parameters.cubemapFace);
                CoreUtils.DrawFullScreen(cmd, parameters.cloudCombinePass, null, 1);
            }

            // Reset all the multi-compiles
            CoreUtils.SetKeyword(cmd, "LOCAL_VOLUMETRIC_CLOUDS", false);
        }

        struct VolumetricCloudsParameters_Sky_High
        {
            // Resolution parameters
            public int finalWidth;
            public int finalHeight;

            // Compute shader and kernels
            public int renderKernel;
            public int combineKernel;

            // Data common to all volumetric cloud passes
            public VolumetricCloudCommonData commonData;

            // Sky
            public bool renderForCubeMap;
            public CubemapFace cubemapFace;
            public Material cloudCombinePass;
        }

        VolumetricCloudsParameters_Sky_High PrepareVolumetricCloudsParameters_Sky_High(HDCamera hdCamera, int width, int height, CubemapFace cubemapFace, VolumetricClouds settings)
        {
            VolumetricCloudsParameters_Sky_High parameters = new VolumetricCloudsParameters_Sky_High();

            // Compute the cloud model data
            CloudModelData cloudModelData = GetCloudModelData(settings);

            // Fill the common data
            FillVolumetricCloudsCommonData(false, settings, TVolumetricCloudsCameraType.Sky, in cloudModelData, ref parameters.commonData);

            // If this is a baked reflection, we run everything at full res
            parameters.finalWidth = width;
            parameters.finalHeight = height;

            // Sky
            parameters.cubemapFace = cubemapFace;
            parameters.cloudCombinePass = m_CloudCombinePass;

            // Compute shader and kernels
            parameters.renderKernel = m_CloudRenderKernel;
            parameters.combineKernel = m_CombineCloudsSkyKernel;

            // Update the constant buffer
            VolumetricCloudsCameraData cameraData;
            cameraData.cameraType = parameters.commonData.cameraType;
            cameraData.traceWidth = parameters.finalWidth;
            cameraData.traceHeight = parameters.finalHeight;
            cameraData.intermediateWidth = parameters.finalWidth;
            cameraData.intermediateHeight = parameters.finalHeight;
            cameraData.finalWidth = parameters.finalWidth;
            cameraData.finalHeight = parameters.finalHeight;
            cameraData.viewCount = 1;
            cameraData.enableExposureControl = parameters.commonData.enableExposureControl;
            cameraData.lowResolution = false;
            cameraData.enableIntegration = false;
            UpdateShaderVariableslClouds(ref parameters.commonData.cloudsCB, hdCamera, settings, cameraData, cloudModelData, false);

            return parameters;
        }

        static void RenderVolumetricClouds_Sky_High(CommandBuffer cmd, in VolumetricCloudsParameters_Sky_High parameters, MaterialPropertyBlock mpb, RTHandle intermediateLightingBuffer0, RTHandle intermediateDepthBuffer0, RTHandle colorBuffer)
        {
            // Compute the number of tiles to evaluate
            int finalTX = (parameters.finalWidth + (8 - 1)) / 8;
            int finalTY = (parameters.finalHeight + (8 - 1)) / 8;

            // Bind the sampling textures
            BlueNoise.BindDitheredTextureSet(cmd, parameters.commonData.ditheredTextureSet);

            // Set the multi compile
            CoreUtils.SetKeyword(cmd, "LOCAL_VOLUMETRIC_CLOUDS", false);

            // Bind the constant buffer
            ConstantBuffer.Push(cmd, parameters.commonData.cloudsCB, parameters.commonData.volumetricCloudsCS, HDShaderIDs._ShaderVariablesClouds);

            // Ray-march the clouds for this frame
            CoreUtils.SetKeyword(cmd, "PHYSICALLY_BASED_SUN", parameters.commonData.cloudsCB._PhysicallyBasedSun == 1);
            cmd.SetComputeTextureParam(parameters.commonData.volumetricCloudsCS, parameters.renderKernel, HDShaderIDs._Worley128RGBA, parameters.commonData.worley128RGBA);
            cmd.SetComputeTextureParam(parameters.commonData.volumetricCloudsCS, parameters.renderKernel, HDShaderIDs._ErosionNoise, parameters.commonData.erosionNoise);
            cmd.SetComputeTextureParam(parameters.commonData.volumetricCloudsCS, parameters.renderKernel, HDShaderIDs._CloudMapTexture, parameters.commonData.cloudMapTexture);
            cmd.SetComputeTextureParam(parameters.commonData.volumetricCloudsCS, parameters.renderKernel, HDShaderIDs._CloudLutTexture, parameters.commonData.cloudLutTexture);
            cmd.SetComputeTextureParam(parameters.commonData.volumetricCloudsCS, parameters.renderKernel, HDShaderIDs._CloudsLightingTextureRW, intermediateLightingBuffer0);
            cmd.SetComputeTextureParam(parameters.commonData.volumetricCloudsCS, parameters.renderKernel, HDShaderIDs._CloudsDepthTextureRW, intermediateDepthBuffer0);
            cmd.DispatchCompute(parameters.commonData.volumetricCloudsCS, parameters.renderKernel, finalTX, finalTY, 1);
            CoreUtils.SetKeyword(cmd, "PHYSICALLY_BASED_SUN", false);

            // Output the result into the output buffer
            mpb.SetTexture(HDShaderIDs._VolumetricCloudsUpscaleTextureRW, intermediateLightingBuffer0);
            CoreUtils.SetRenderTarget(cmd, colorBuffer, ClearFlag.None, 0, parameters.cubemapFace);
            CoreUtils.DrawFullScreen(cmd, parameters.cloudCombinePass, mpb, 1);
        }

        internal void RenderVolumetricClouds_Sky(CommandBuffer cmd, HDCamera hdCamera, Matrix4x4[] pixelCoordToViewDir, VolumetricClouds settings, int width, int height, RTHandle skyboxCubemap)
        {
            // If the current volume does not enable the feature, quit right away.
            if (!HasVolumetricClouds(hdCamera, in settings))
                return;

            if (hdCamera.frameSettings.IsEnabled(FrameSettingsField.FullResolutionCloudsForSky))
            {
                // Prepare the parameters
                VolumetricCloudsParameters_Sky_High parameters = PrepareVolumetricCloudsParameters_Sky_High(hdCamera, width, height, CubemapFace.Unknown, settings);

                using (new ProfilingScope(cmd, ProfilingSampler.Get(HDProfileId.VolumetricCloudsTrace)))
                {
                    for (int faceIdx = 0; faceIdx < 6; ++faceIdx)
                    {
                        // Update the cubemap face and the inverse projection matrix
                        parameters.cubemapFace = (CubemapFace)faceIdx;
                        parameters.commonData.cloudsCB._CloudsPixelCoordToViewDirWS = pixelCoordToViewDir[faceIdx];

                        // Render the face straight to the output cubemap
                        RenderVolumetricClouds_Sky_High(cmd, parameters, m_MpbClouds, m_IntermediateCloudsLightingBuffer, m_IntermediateCloudsDepthBuffer, skyboxCubemap);
                    }
                }
            }
            else
            {
                VolumetricCloudsParameters_Sky_Low parameters = PrepareVolumetricCloudsParameters_Sky_Low(hdCamera, width, height, CubemapFace.Unknown, settings);

                using (new ProfilingScope(cmd, ProfilingSampler.Get(HDProfileId.VolumetricCloudsTrace)))
                {
                    for (int faceIdx = 0; faceIdx < 6; ++faceIdx)
                    {
                        // Update the cubemap face and the inverse projection matrix
                        parameters.cubemapFace = (CubemapFace)faceIdx;
                        parameters.commonData.cloudsCB._CloudsPixelCoordToViewDirWS = pixelCoordToViewDir[faceIdx];

                        // Render the face straight to the output cubemap
                        TraceVolumetricClouds_Sky_Low(cmd, parameters, m_MpbClouds, m_IntermediateCloudsLightingBuffer, m_IntermediateCloudsDepthBuffer, m_IntermediateCloudsLightingCube0Buffer);
                    }
                }

                using (new ProfilingScope(cmd, ProfilingSampler.Get(HDProfileId.VolumetricCloudsPreUpscale)))
                {
                    for (int faceIdx = 0; faceIdx < 6; ++faceIdx)
                    {
                        m_MpbClouds.SetTexture(HDShaderIDs._VolumetricCloudsTexture, m_IntermediateCloudsLightingCube0Buffer);
                        m_MpbClouds.SetMatrix(HDShaderIDs._PixelCoordToViewDirWS, pixelCoordToViewDir[faceIdx]);
                        m_MpbClouds.SetInt(HDShaderIDs._Mipmap, 2);
                        CoreUtils.SetRenderTarget(cmd, m_IntermediateCloudsLightingCube1Buffer, ClearFlag.None, 1, (CubemapFace)faceIdx);
                        CoreUtils.DrawFullScreen(cmd, parameters.cloudCombinePass, m_MpbClouds, 3);
                    }
                }

                using (new ProfilingScope(cmd, ProfilingSampler.Get(HDProfileId.VolumetricCloudsUpscaleAndCombine)))
                {
                    for (int faceIdx = 0; faceIdx < 6; ++faceIdx)
                    {
                        m_MpbClouds.SetTexture(HDShaderIDs._VolumetricCloudsTexture, m_IntermediateCloudsLightingCube1Buffer);
                        m_MpbClouds.SetMatrix(HDShaderIDs._PixelCoordToViewDirWS, pixelCoordToViewDir[faceIdx]);
                        m_MpbClouds.SetInt(HDShaderIDs._Mipmap, 1);
                        CoreUtils.SetRenderTarget(cmd, skyboxCubemap, ClearFlag.None, 0, (CubemapFace)faceIdx);
                        CoreUtils.DrawFullScreen(cmd, parameters.cloudCombinePass, m_MpbClouds, 4);
                    }
                }
            }
        }
    }
}
