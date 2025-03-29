using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using FFmpeg.AutoGen;
using ParrotMimicry.Models;

namespace ParrotMimicry.Services
{
    public class VideoToAudioService
    {
        private static readonly string FFmpegPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "FFmpeg");
        private static bool _isInitialized = false;

        public VideoToAudioService()
        {
            InitializeFFmpeg();
        }

        private void InitializeFFmpeg()
        {
            if (_isInitialized) return;

            try
            {
                // 设置FFmpeg库的路径
                ffmpeg.RootPath = FFmpegPath;
                _isInitialized = true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"FFmpeg初始化失败: {ex.Message}");
                throw new Exception("FFmpeg初始化失败，请确保FFmpeg库已正确安装", ex);
            }
        }

        /// <summary>
        /// 将视频文件转换为音频文件
        /// </summary>
        /// <param name="videoFile">视频文件模型</param>
        /// <param name="outputPath">输出音频文件路径，如果为null则使用默认路径</param>
        /// <param name="progressCallback">进度回调</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>转换后的音频文件路径</returns>
        public async Task<string> ConvertVideoToAudioAsync(
            VideoFile videoFile, 
            string outputPath = null, 
            IProgress<double> progressCallback = null, 
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(videoFile.FilePath) || !File.Exists(videoFile.FilePath))
            {
                throw new FileNotFoundException("视频文件不存在", videoFile.FilePath);
            }

            // 如果未指定输出路径，则使用默认路径
            if (string.IsNullOrEmpty(outputPath))
            {
                string directory = Path.GetDirectoryName(videoFile.FilePath);
                string fileName = Path.GetFileNameWithoutExtension(videoFile.FilePath);
                outputPath = Path.Combine(directory, $"{fileName}.wav");
            }

            return await Task.Run(() =>
            {
                // 声明FFmpeg相关变量
                unsafe
                {
                    AVFormatContext* inputFormatContext = null;
                    AVFormatContext* outputFormatContext = null;
                    AVCodecContext* inputCodecContext = null;
                    AVCodecContext* outputCodecContext = null;
                    SwrContext* swrContext = null;
                    AVFrame* frame = null;
                    AVPacket* packet = null;
                    int audioStreamIndex = -1;
                    long totalDuration = 0;
                    long processedDuration = 0;

                    try
                    {
                        videoFile.IsConverting = true;
                        videoFile.Progress = 0;

                        // 分配AVFormatContext
                        inputFormatContext = ffmpeg.avformat_alloc_context();
                        if (inputFormatContext == null)
                            throw new Exception("无法分配输入格式上下文");

                        // 打开输入文件
                        fixed (byte* inputPathPtr = System.Text.Encoding.UTF8.GetBytes(videoFile.FilePath + "\0"))
                        {
                            int ret = ffmpeg.avformat_open_input(&inputFormatContext, videoFile.FilePath, null, null);
                            if (ret < 0)
                                throw new Exception($"无法打开输入文件: {GetErrorMessage(ret)}");
                        }

                        // 获取流信息
                        int infoRet = ffmpeg.avformat_find_stream_info(inputFormatContext, null);
                        if (infoRet < 0)
                            throw new Exception($"无法获取流信息: {GetErrorMessage(infoRet)}");

                        // 查找音频流
                        audioStreamIndex = ffmpeg.av_find_best_stream(inputFormatContext, AVMediaType.AVMEDIA_TYPE_AUDIO, -1, -1, null, 0);
                        if (audioStreamIndex < 0)
                            throw new Exception("未找到音频流");

                        // 已经在上面检查过audioStreamIndex < 0的情况，这里不需要重复检查

                        // 获取总时长（微秒）
                        totalDuration = inputFormatContext->duration > 0 ?
                            inputFormatContext->duration :
                            (long)(inputFormatContext->streams[audioStreamIndex]->duration *
                            ffmpeg.av_q2d(inputFormatContext->streams[audioStreamIndex]->time_base) *
                            ffmpeg.AV_TIME_BASE);

                        // 获取解码器
                        AVCodecParameters* codecParams = inputFormatContext->streams[audioStreamIndex]->codecpar;
                        AVCodec* decoder = ffmpeg.avcodec_find_decoder(codecParams->codec_id);
                        if (decoder == null)
                            throw new Exception("未找到合适的解码器");

                        // 分配解码器上下文
                        inputCodecContext = ffmpeg.avcodec_alloc_context3(decoder);
                        if (inputCodecContext == null)
                            throw new Exception("无法分配解码器上下文");

                        // 复制编解码器参数到上下文
                        int paramRet = ffmpeg.avcodec_parameters_to_context(inputCodecContext, codecParams);
                        if (paramRet < 0)
                            throw new Exception($"无法复制编解码器参数: {GetErrorMessage(paramRet)}");

                        // 打开解码器
                        int openRet = ffmpeg.avcodec_open2(inputCodecContext, decoder, null);
                        if (openRet < 0)
                            throw new Exception($"无法打开解码器: {GetErrorMessage(openRet)}");

                        // 创建输出格式上下文
                        int formatRet = ffmpeg.avformat_alloc_output_context2(&outputFormatContext, null, null, outputPath);
                        if (formatRet < 0 || outputFormatContext == null)
                            throw new Exception($"无法创建输出格式上下文: {GetErrorMessage(formatRet)}");

                        // 添加音频流
                        AVStream* outputStream = ffmpeg.avformat_new_stream(outputFormatContext, null);
                        if (outputStream == null)
                            throw new Exception("无法创建输出流");

                        // 查找编码器
                        AVCodec* encoder = ffmpeg.avcodec_find_encoder(AVCodecID.AV_CODEC_ID_PCM_S16LE); // WAV格式
                        if (encoder == null)
                            throw new Exception("未找到合适的编码器");

                        // 分配编码器上下文
                        outputCodecContext = ffmpeg.avcodec_alloc_context3(encoder);
                        if (outputCodecContext == null)
                            throw new Exception("无法分配编码器上下文");

                        // 设置编码参数
                        outputCodecContext->sample_fmt = AVSampleFormat.AV_SAMPLE_FMT_S16;
                        outputCodecContext->sample_rate = inputCodecContext->sample_rate;
                        
                        // 正确设置通道布局
                        if (inputCodecContext->ch_layout.nb_channels != 0)
                        {
                            // 使用新的ch_layout结构
                            ffmpeg.av_channel_layout_copy(&outputCodecContext->ch_layout, &inputCodecContext->ch_layout);
                            // 为了向后兼容，同时设置channel_layout
                            outputCodecContext->ch_layout.u.mask = outputCodecContext->ch_layout.u.mask;
                        }
                        else
                        {
                            // 在新版FFmpeg中，应该使用ch_layout结构体而不是直接访问channel_layout
                            // 默认使用2通道立体声
                            int nb_channels = 2;
                            
                            // 使用新版FFmpeg API设置通道布局
                            ffmpeg.av_channel_layout_default(&outputCodecContext->ch_layout, nb_channels);
                            
                            // 为了向后兼容，同时设置channel_layout
                            outputCodecContext->ch_layout.u.mask     = outputCodecContext->ch_layout.u.mask;
                            // 确保通道数量一致
                            outputCodecContext->ch_layout.nb_channels = nb_channels;
                        }
                        outputCodecContext->time_base = new AVRational { num = 1, den = outputCodecContext->sample_rate };
                        outputCodecContext->strict_std_compliance = -2; // 允许实验性编码器

                        // 复制流参数到输出流
                        int streamRet = ffmpeg.avcodec_parameters_from_context(outputStream->codecpar, outputCodecContext);
                        if (streamRet < 0)
                            throw new Exception($"无法复制流参数: {GetErrorMessage(streamRet)}");

                        // 打开编码器
                        int encodeRet = ffmpeg.avcodec_open2(outputCodecContext, encoder, null);
                        if (encodeRet < 0)
                            throw new Exception($"无法打开编码器: {GetErrorMessage(encodeRet)}");

                        // 创建重采样上下文
                        swrContext = ffmpeg.swr_alloc();
                        if (swrContext == null)
                            throw new Exception("无法分配重采样上下文");

                        // 设置重采样参数 - 使用FFmpeg 7.0兼容的API
                        // 使用新的ch_layout API设置重采样上下文
                        
                        // 使用新的API设置重采样上下文参数
                        ffmpeg.swr_alloc_set_opts2(&swrContext, 
                                                  &outputCodecContext->ch_layout, 
                                                  outputCodecContext->sample_fmt, 
                                                  outputCodecContext->sample_rate,
                                                  &inputCodecContext->ch_layout, 
                                                  inputCodecContext->sample_fmt, 
                                                  inputCodecContext->sample_rate, 
                                                  0, null);
                        // 不再需要单独设置这些参数，因为swr_alloc_set_opts2已经设置了它们
                        // 但为了确保兼容性，我们保留这些设置
                        ffmpeg.av_opt_set_int(swrContext, "in_sample_rate", inputCodecContext->sample_rate, 0);
                        ffmpeg.av_opt_set_int(swrContext, "out_sample_rate", outputCodecContext->sample_rate, 0);
                        ffmpeg.av_opt_set_sample_fmt(swrContext, "in_sample_fmt", inputCodecContext->sample_fmt, 0);
                        ffmpeg.av_opt_set_sample_fmt(swrContext, "out_sample_fmt", outputCodecContext->sample_fmt, 0);

                        // 初始化重采样上下文
                        int swrRet = ffmpeg.swr_init(swrContext);
                        if (swrRet < 0)
                            throw new Exception($"无法初始化重采样上下文: {GetErrorMessage(swrRet)}");

                        // 打开输出文件
                        if ((outputFormatContext->oformat->flags & ffmpeg.AVFMT_NOFILE) == 0)
                        {
                            int ioRet = ffmpeg.avio_open(&outputFormatContext->pb, outputPath, ffmpeg.AVIO_FLAG_WRITE);
                            if (ioRet < 0)
                                throw new Exception($"无法打开输出文件: {GetErrorMessage(ioRet)}");
                        }

                        // 写入文件头
                        int headerRet = ffmpeg.avformat_write_header(outputFormatContext, null);
                        if (headerRet < 0)
                            throw new Exception($"无法写入文件头: {GetErrorMessage(headerRet)}");

                        // 分配帧和包
                        frame = ffmpeg.av_frame_alloc();
                        packet = ffmpeg.av_packet_alloc();
                        if (frame == null || packet == null)
                            throw new Exception("无法分配帧或包");

                        // 开始转换
                        int readRet = 0;
                        while ((readRet = ffmpeg.av_read_frame(inputFormatContext, packet)) >= 0)
                        {
                            // 检查取消令牌
                            if (cancellationToken.IsCancellationRequested)
                            {
                                ffmpeg.av_packet_unref(packet);
                                throw new OperationCanceledException("转换操作已取消");
                            }

                            // 只处理音频流
                            if (packet->stream_index == audioStreamIndex)
                            {
                                // 发送包到解码器
                                int sendRet = ffmpeg.avcodec_send_packet(inputCodecContext, packet);
                                if (sendRet < 0)
                                {
                                    ffmpeg.av_packet_unref(packet);
                                    throw new Exception($"发送包到解码器失败: {GetErrorMessage(sendRet)}");
                                }

                                // 接收解码后的帧
                                int receiveRet = 0;
                                while ((receiveRet = ffmpeg.avcodec_receive_frame(inputCodecContext, frame)) >= 0)
                                {
                                    // 更新进度
                                    if (totalDuration > 0)
                                    {
                                        processedDuration = (long)(frame->pts * ffmpeg.av_q2d(inputFormatContext->streams[audioStreamIndex]->time_base) * ffmpeg.AV_TIME_BASE);
                                        double progress = Math.Min(1.0, Math.Max(0.0, (double)processedDuration / totalDuration));
                                        videoFile.Progress = progress;
                                        progressCallback?.Report(progress);
                                    }

                                    // 重采样
                                    AVFrame* outFrame = ffmpeg.av_frame_alloc();
                                    if (outFrame == null)
                                        throw new Exception("无法分配输出帧");

                                    // 正确设置输出帧参数
                                    outFrame->nb_samples = outputCodecContext->frame_size > 0 ?
                                        outputCodecContext->frame_size :
                                        frame->nb_samples;
                                    outFrame->format = (int)outputCodecContext->sample_fmt;
                                    outFrame->sample_rate = outputCodecContext->sample_rate;
                                    
                                    // 使用新的ch_layout API设置通道布局
                                    ffmpeg.av_channel_layout_copy(&outFrame->ch_layout, &outputCodecContext->ch_layout);
                                    // 保持向后兼容性，但在新版FFmpeg中，channel_layout可能已被弃用
                                    // 使用ch_layout.u.mask获取通道布局掩码
                                    outFrame->ch_layout.u.mask = outputCodecContext->ch_layout.u.mask;
                                    // 在新版FFmpeg中，不应直接设置channels属性
                                    // 而是通过ch_layout结构体访问通道信息

                                    int bufferRet = ffmpeg.av_frame_get_buffer(outFrame, 0);
                                    if (bufferRet < 0)
                                    {
                                        ffmpeg.av_frame_free(&outFrame);
                                        throw new Exception($"无法分配输出帧缓冲区: {GetErrorMessage(bufferRet)}");
                                    }

                                    // 执行重采样
                                    int convertRet = ffmpeg.swr_convert_frame(swrContext, outFrame, frame);
                                    if (convertRet < 0)
                                    {
                                        ffmpeg.av_frame_free(&outFrame);
                                        throw new Exception($"重采样失败: {GetErrorMessage(convertRet)}");
                                    }

                                    // 发送帧到编码器
                                    int encodeFrameRet = ffmpeg.avcodec_send_frame(outputCodecContext, outFrame);
                                    if (encodeFrameRet < 0)
                                    {
                                        ffmpeg.av_frame_free(&outFrame);
                                        throw new Exception($"发送帧到编码器失败: {GetErrorMessage(encodeFrameRet)}");
                                    }

                                    // 释放输出帧，因为已经发送到编码器
                                    ffmpeg.av_frame_free(&outFrame);

                                    // 接收编码后的包
                                    while (true)
                                    {
                                        AVPacket* outPacket = ffmpeg.av_packet_alloc();
                                        if (outPacket == null)
                                            throw new Exception("无法分配输出包");

                                        int outReceiveRet = ffmpeg.avcodec_receive_packet(outputCodecContext, outPacket);
                                        if (outReceiveRet == ffmpeg.AVERROR(ffmpeg.EAGAIN) || outReceiveRet == ffmpeg.AVERROR_EOF)
                                        {
                                            ffmpeg.av_packet_free(&outPacket);
                                            break;
                                        }
                                        else if (outReceiveRet < 0)
                                        {
                                            ffmpeg.av_packet_free(&outPacket);
                                            throw new Exception($"接收包失败: {GetErrorMessage(outReceiveRet)}");
                                        }

                                        // 写入包到输出文件
                                        int writeRet = ffmpeg.av_interleaved_write_frame(outputFormatContext, outPacket);
                                        // 注意：av_interleaved_write_frame会接管packet的内存，所以不需要手动释放
                                        if (writeRet < 0)
                                        {
                                            ffmpeg.av_packet_free(&outPacket);
                                            throw new Exception($"写入包失败: {GetErrorMessage(writeRet)}");
                                        }
                                    }

                                    // 重置帧以便下次使用
                                    ffmpeg.av_frame_unref(frame);
                                }
                            }

                            ffmpeg.av_packet_unref(packet);
                        }

                        // 刷新编码器
                        int flushRet = ffmpeg.avcodec_send_frame(outputCodecContext, null);
                        if (flushRet < 0)
                        {
                            throw new Exception($"刷新编码器失败: {GetErrorMessage(flushRet)}");
                        }

                        // 接收并处理所有剩余的编码包
                        while (true)
                        {
                            AVPacket* outPacket = ffmpeg.av_packet_alloc();
                            if (outPacket == null)
                                throw new Exception("无法分配输出包");

                            int flushReceiveRet = ffmpeg.avcodec_receive_packet(outputCodecContext, outPacket);
                            if (flushReceiveRet == ffmpeg.AVERROR(ffmpeg.EAGAIN) || flushReceiveRet == ffmpeg.AVERROR_EOF)
                            {
                                ffmpeg.av_packet_free(&outPacket);
                                break;
                            }
                            else if (flushReceiveRet < 0)
                            {
                                ffmpeg.av_packet_free(&outPacket);
                                throw new Exception($"刷新编码器接收包失败: {GetErrorMessage(flushReceiveRet)}");
                            }

                            // 写入包到输出文件
                            int writeRet = ffmpeg.av_interleaved_write_frame(outputFormatContext, outPacket);
                            ffmpeg.av_packet_free(&outPacket);
                            if (writeRet < 0)
                                throw new Exception($"写入包失败: {GetErrorMessage(writeRet)}");
                        }

                        // 写入文件尾
                        int trailerRet = ffmpeg.av_write_trailer(outputFormatContext);
                        if (trailerRet < 0)
                            throw new Exception($"无法写入文件尾: {GetErrorMessage(trailerRet)}");

                        // 设置进度为100%
                        videoFile.Progress = 1.0;
                        progressCallback?.Report(1.0);

                        return outputPath;
                    }
                    catch (Exception ex) when (!(ex is OperationCanceledException))
                    {
                        throw new Exception($"视频转换为音频失败: {ex.Message}", ex);
                    }
                    finally
                    {
                        // 释放资源
                        unsafe
                        {
                            // 确保按照正确的顺序释放资源，避免内存泄漏
                            if (packet != null)
                            {
                                ffmpeg.av_packet_unref(packet); // 先解引用
                                ffmpeg.av_packet_free(&packet); // 再释放
                            }

                            if (frame != null)
                            {
                                ffmpeg.av_frame_unref(frame); // 先解引用
                                ffmpeg.av_frame_free(&frame); // 再释放
                            }

                            // 释放重采样上下文
                            if (swrContext != null)
                                ffmpeg.swr_free(&swrContext);

                            if (outputCodecContext != null)
                                ffmpeg.avcodec_free_context(&outputCodecContext);

                            if (inputCodecContext != null)
                                ffmpeg.avcodec_free_context(&inputCodecContext);

                            if (outputFormatContext != null)
                            {
                                if (outputFormatContext->oformat != null &&
                                    (outputFormatContext->oformat->flags & ffmpeg.AVFMT_NOFILE) == 0 &&
                                    outputFormatContext->pb != null)
                                {
                                    ffmpeg.avio_closep(&outputFormatContext->pb);
                                }
                                ffmpeg.avformat_free_context(outputFormatContext);
                            }

                            if (inputFormatContext != null)
                                ffmpeg.avformat_close_input(&inputFormatContext);
                        }

                        // 无论成功与否，都将转换状态设为false
                        videoFile.IsConverting = false;
                    }
                }
            }, cancellationToken);
        }

        /// <summary>
        /// 批量转换视频文件为音频文件
        /// </summary>
        /// <param name="videoFiles">视频文件列表</param>
        /// <param name="outputDirectory">输出目录，如果为null则使用原视频文件所在目录</param>
        /// <param name="progressCallback">总体进度回调</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>转换后的音频文件路径列表</returns>
        /// <summary>
        /// 获取FFmpeg错误信息
        /// </summary>
        /// <param name="error">错误码</param>
        /// <returns>错误信息</returns>
        private unsafe string GetErrorMessage(int error)
        {
            const int bufferSize = 1024;
            byte* buffer = stackalloc byte[bufferSize];
            ffmpeg.av_strerror(error, buffer, bufferSize);
            return Marshal.PtrToStringAnsi((IntPtr)buffer);
        }

        public async Task<List<string>> BatchConvertVideoToAudioAsync(
            List<VideoFile> videoFiles, 
            string outputDirectory = null, 
            IProgress<double> progressCallback = null, 
            CancellationToken cancellationToken = default)
        {
            List<string> outputPaths = new List<string>();
            int totalFiles = videoFiles.Count;
            int completedFiles = 0;

            foreach (var videoFile in videoFiles)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    break;
                }

                string outputPath = null;
                if (!string.IsNullOrEmpty(outputDirectory))
                {
                    string fileName = Path.GetFileNameWithoutExtension(videoFile.FilePath);
                    outputPath = Path.Combine(outputDirectory, $"{fileName}.wav");
                }

                // 创建一个进度报告转换器，将单个文件的进度转换为总体进度
                var fileProgress = new Progress<double>(p =>
                {
                    double overallProgress = (completedFiles + p) / totalFiles;
                    progressCallback?.Report(overallProgress);
                });

                try
                {
                    string result = await ConvertVideoToAudioAsync(videoFile, outputPath, fileProgress, cancellationToken);
                    outputPaths.Add(result);
                }
                catch (Exception ex)
                {
                    // 记录错误但继续处理其他文件
                    Console.WriteLine($"转换文件 {videoFile.FileName} 失败: {ex.Message}");
                }

                completedFiles++;
                progressCallback?.Report((double)completedFiles / totalFiles);
            }

            return outputPaths;
        }
    }
}