using System;
using System.IO;
using System.Windows;
using System.Text.RegularExpressions;
using System.Collections;

namespace FFmepgTools
{
    /// <summary>
    /// MainWindow.xaml 的交互逻辑
    /// </summary>
    public partial class MainWindow : Window
    {
        public string FfMpegExe;
        public string FfPlayExe;
        public string Input = "";

        public MainWindow()
        {
            InitializeComponent();
            // 1. 先在命令行PATH中找ffmpeg
            // 2. 其次在程序执行目录中ffmpeg
            // 3. 都没找到退出

            FfMpegExe = GetFullPath("ffmpeg.exe");
            FfPlayExe = GetFullPath("ffplay.exe");

            if (FfMpegExe == null) FfMpegExe = AppDomain.CurrentDomain.BaseDirectory + "ffmpeg.exe";
            if (FfPlayExe == null) FfPlayExe = AppDomain.CurrentDomain.BaseDirectory + "ffplay.exe";

            if (File.Exists(FfMpegExe) == false)
            {
                MessageBox.Show("未找到[ffmpeg.exe]，工具将无法使用", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                System.Windows.Application.Current.Shutdown();
            }

            if (File.Exists(FfPlayExe) == false)
            {
                MessageBox.Show("未找到[ffplay.exe]，播放功能无法使用", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 返回唯一的时间戳
        /// </summary>
        /// <returns></returns>
        private string getFileKey()
        {
            return new DateTimeOffset(DateTime.UtcNow).ToUnixTimeSeconds().ToString();
        }

        private string GetFullPath(string fileName)
        {
            if (File.Exists(fileName))
                return Path.GetFullPath(fileName);

            var values = Environment.GetEnvironmentVariable("PATH");
            if (values == null)
                return null;

            foreach (var path in values.Split(Path.PathSeparator))
            {
                var fullPath = Path.Combine(path, fileName);
                if (File.Exists(fullPath))
                    return fullPath;
            }

            return null;
        }

        private void Drop1(object sender, DragEventArgs e)
        {
            string[] filaments = (string[])e.Data.GetData(DataFormats.FileDrop);
            if (filaments != null && filaments.Length > 0)
            {
                Input = filaments[0];
                InputFile.Text = Input;
            }
        }

        private void Drop2(object sender, DragEventArgs e)
        {
            string[] filaments = (string[])e.Data.GetData(DataFormats.FileDrop);
            if (filaments != null && filaments.Length > 0)
            {
                InputAudio.Text = filaments[0];
            }
        }

        /// <summary>
        /// 带控制台，方便看到更多信息,和错误
        /// </summary>
        /// <param name="command"></param>
        private void Execute(string command)
        {
            try
            {
                System.Diagnostics.Process.Start(FfMpegExe, command);
            }
            catch (Exception)
            {
                MessageBox.Show("执行错误: " + command);
            }
        }

        /// <summary>
        /// 将处理后的资源，写入相同目录下
        /// </summary>
        /// <returns></returns>
        private string GetOutputFilepath(string extension = "")
        {
            if (Input.Length == 0) return null;
            extension = extension.Length != 0 ? extension : Path.GetExtension(Input);

            // xxx/xxx/[time]oldname.extension
            return Path.Combine(Path.GetDirectoryName(Input) ?? string.Empty,
                $"[{getFileKey()}]-{Path.GetFileNameWithoutExtension(Input)}{extension}");
        }

        /// <summary>
        /// 选择文件视频
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ChooseFileButton_Click(object sender, RoutedEventArgs e)
        {
            var openFileDialog = new Microsoft.Win32.OpenFileDialog()
            {
                Filter = "(*.*)|*.*"
            };

            if (openFileDialog.ShowDialog() == false) return;
            Input = openFileDialog.FileName;
            InputFile.Text = Input;
        }

        private bool CheckNotInputFile()
        {
            if (Input.Length == 0)
            {
                MessageBox.Show("没有输入文件", "错误", MessageBoxButton.OK, MessageBoxImage.Warning);
                return true;
            }

            return false;
        }

        /// <summary>
        /// 去掉视频音轨
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void VideoTrackCloseButton_Click(object sender, RoutedEventArgs e)
        {
            if (CheckNotInputFile()) return;
            string output = GetOutputFilepath();
            // ffmpeg -i "demo.mp4" -vcodec copy -an "out.mp4"
            string command = "-i \"" + Input + "\" -vcodec copy -an \"" + output + "\"";
            Execute(command);
        }

        /// <summary>
        /// 提取视频音轨
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void VideoTrackGetButton_Click(object sender, RoutedEventArgs e)
        {
            if (CheckNotInputFile()) return;
            string output = GetOutputFilepath(".mp3");
            // ffmpeg -i demo.mp4  out.mp3
            string command = "-i \"" + Input + "\"  \"" + output + "\"";
            Execute(command);
        }

        /// <summary>
        /// 调整音量
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void VolumeButton_Click(object sender, RoutedEventArgs e)
        {
            if (CheckNotInputFile()) return;
            if (InputVolume.Text.Length == 0) return;
            string output = GetOutputFilepath();

            // 视频不变，减少音量
            // ffmpeg - i input.mp4 -vcodec copy -af "volume=-10dB"  out.mp4
            string command = "-i \"" + Input + "\" -vcodec copy -af \"volume=" + InputVolume.Text + "\" \"" + output +
                             "\"";
            Execute(command);
        }

        /// <summary>
        /// 使用ffplay播放输入资源，视频，音频，图片，直播流...
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void PlayButton_Click(object sender, RoutedEventArgs e)
        {
            if (CheckNotInputFile()) return;
            ArrayList options = new ArrayList();
            if (IsLoopPlay.IsChecked == true) options.Add("-loop 0");
            if (TVn.IsChecked == true) options.Add("-vn");
            if (TAn.IsChecked == true) options.Add("-an");
            if (NoBorder.IsChecked == true) options.Add("-noborder");

            string[] size = TSize.Text.Split('x');
            if (size.Length == 2)
            {
                if (size[0].Length != 0) options.Add($"-x {size[0]}");
                if (size[1].Length != 0) options.Add($"-y {size[1]}");
            }

            options.Add($"\"{Input}\"");
            string command = string.Join(" ", (string[])options.ToArray(Type.GetType("System.String")));
            System.Diagnostics.Process.Start(FfPlayExe, command);
        }

        /// <summary>
        /// 音频填充视频
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void AudioFillVideoButton_Click(object sender, RoutedEventArgs e)
        {

            if (CheckNotInputFile() || InputAudio.Text.Length == 0) return;
            string output = GetOutputFilepath();

            // 将10s视频和5s音频合并，输出视频有10s,音频将一直循环
            // ffmpeg -i input.mp4 -stream_loop -1 -i input.mp3 -c copy -map 0:v:0 -map 1:a:0 -shortest out.mp4
            string command =
                $"-i \"{Input}\" -stream_loop -1 -i \"{InputAudio.Text}\" -c copy -map 0:v:0 -map 1:a:0 -shortest \"{output}\"";

            if (IsMix.IsChecked == true)
            {
                // ffmpeg -i 4.mp4 -i a1.mp3 -c:v copy -filter_complex amix -map 0:v -map 0:a -map 1:a -shortest o.mp4
                command =
                    $"-i \"{Input}\" -stream_loop -1 -i \"{InputAudio.Text}\" -c:v copy -filter_complex amix -map 0:v:0 -map 0:a:0 -map 1:a:0 -shortest \"{output}\"";
            }

            Execute(command);
        }

        /// <summary>
        /// 视频填充音频 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void VideoFillAudioButton_Click(object sender, RoutedEventArgs e)
        {
            if (CheckNotInputFile()) return;
            if (InputAudio.Text.Length == 0) return;

            string output = GetOutputFilepath();

            // 将5s视频和10s音频合并，输出视频有10s,视频将一直循环
            // ffmpeg -stream_loop -1 -i input.mp4 -i input.mp3 -c copy -map 0:v:0 -map 1:a:0 -shortest out.mp4
            string command =
                $"-stream_loop -1 -i \"{Input}\" -i \"{InputAudio.Text}\" -c copy -map 0:v:0 -map 1:a:0 -shortest \"{output}\"";

            if (IsMix.IsChecked == true)
            {
                MessageBox.Show("此功能无法执行混音", "错误", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            Execute(command);
        }

        /// <summary>
        /// 选择音频文件
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ChooseAudioButton_Click(object sender, RoutedEventArgs e)
        {
            var openFileDialog = new Microsoft.Win32.OpenFileDialog()
            {
                Filter = "(*.*)|*.*"
            };
            if (openFileDialog.ShowDialog() == false) return;
            InputAudio.Text = openFileDialog.FileName;
        }

        /// <summary>
        /// 从开始处裁剪指定时间
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void TailoringButton_Click(object sender, RoutedEventArgs e)
        {

            if (CheckNotInputFile()) return;
            string output = GetOutputFilepath();

            // ffmpeg -i input.mp4 -ss 00:00:00 -t 10 -c copy 1.mp4
            string command = $"-i \"{Input}\" -ss {C10.Text} -t {C11.Text} -c copy \"{output}\"";
            Execute(command);
        }

        /// <summary>
        /// 时间段裁剪
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Tailoring2Button_Click(object sender, RoutedEventArgs e)
        {

            if (CheckNotInputFile()) return;
            string output = GetOutputFilepath();

            DateTime start = DateTime.Parse(C20.Text);
            DateTime end = DateTime.Parse(C21.Text);
            int t = (end.Second - start.Second);
            if (t < 0)
            {
                MessageBox.Show("切割时间不能为负!!!");
                return;
            }

            // ffmpeg -i input.mp4 -ss 00:00:00 -t 10 -c copy 1.mp3
            string command = $"-i \"{Input}\" -ss {C20.Text} -t {t} -c copy \"{output}\"";
            Execute(command);
        }

        /// <summary>
        /// 从指定时间道结束
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Tailoring3Button_Click(object sender, RoutedEventArgs e)
        {
            if (CheckNotInputFile()) return;
            string output = GetOutputFilepath();

            // 从1小时到结尾
            // ffmpeg -ss 01:00:00 -i m.mp4 -c copy out.mp4
            string command = "-ss " + C30.Text + " -i \"" + Input + "\" -c copy \"" + output + "\"";
            Execute(command);
        }

        private void LookCmdButton_Click(object sender, RoutedEventArgs e)
        {
            System.Diagnostics.Process.Start(
                "https://www.linuxuprising.com/2020/01/ffmpeg-how-to-crop-videos-with-examples.html");
        }

        /// <summary>
        /// 裁剪视频
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void TailoringVideoButton_Click(object sender, RoutedEventArgs e)
        {
            if (CheckNotInputFile()) return;
            string output = GetOutputFilepath();

            // ffmpeg -i input.mp4 -filter:v "crop=w:h:x:y" -c:a copy output.mp4
            string command = "-i \"" + Input + "\" -filter:v \"crop=" + CIw.Text + ":" + CIh.Text + ":" + CIx.Text +
                             ":" +
                             CIy.Text + "\" -c:a copy \"" + output + "\"";
            Execute(command);
        }

        /// <summary>
        /// 将视频分为多个片
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void StartShardButton_Click(object sender, RoutedEventArgs e)
        {
            if (CheckNotInputFile()) return;
            string cname = Path.GetFileNameWithoutExtension(Input);
            string outDir = Path.Combine(Path.GetDirectoryName(Input) ?? string.Empty, $"[{getFileKey()}]-{cname}");
            Directory.CreateDirectory(outDir);

            string segmentation = Path.Combine(outDir, $"%0{SNum.Text}d.{Regex.Replace(SIoExt.Text, @"^\.+", "")}");

            // ffmpeg -i input.mp4 -c copy -map 0 -segment_time 00:01:00 -f segment out-%03d.ts
            string command = $"-i \"{Input}\" -c copy -map 0 -segment_time {SIss.Text} -f segment \"{segmentation}\"";
            Execute(command);
        }

        /// <summary>
        /// 用户提供合并文件，然后合并分片
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void MergaShardButton_Click(object sender, RoutedEventArgs e)
        {
            if (CheckNotInputFile()) return;
            string outfile = Path.Combine(Path.GetDirectoryName(Input) ?? string.Empty, $"new-{getFileKey()}.mp4");

            // ffmpeg -f concat -safe 0 -i i.txt -c copy out.mp4
            string command = $"-f concat -safe 0 -i \"{Input}\" -c copy \"{outfile}\"";
            Execute(command);
        }

        /// <summary>
        /// 生成视频合成配置文件
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void SaveConfigButton_Click(object sender, RoutedEventArgs e)
        {
            if (CheckNotInputFile()) return;
            string odor = Path.GetDirectoryName(Input);
            string extension = Path.GetExtension(Input);
            if (odor != null)
            {
                string lockfile = Path.Combine(odor, "merge.txt");

                // (for %i in (*.ts) do @echo file 'file:%cd%\%i') > mylist.txt
                string command = $"/C (for %i in (\"{odor}\\*{extension}\") do @echo file 'file:%i') > \"{lockfile}\"";
                System.Diagnostics.Process.Start("cmd.exe", command);
            }
        }

        /// <summary>
        /// gif to mp4
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void GifToMp4Button_Click(object sender, RoutedEventArgs e)
        {

            if (CheckNotInputFile()) return;
            string output = GetOutputFilepath(".mp4");

            // ffmpeg -i input.gif -b:v 0 -crf 20 -vf "pad=ceil(iw/2)*2:ceil(ih/2)*2" -vcodec libx264 -pix_fmt yuv420p -f mp4 out2.mp4
            string command = $"-i \"" + Input +
                             "\" -b:v 0 -crf 20 -vf \"pad=ceil(iw/2)*2:ceil(ih/2)*2\" -vcodec libx264 -pix_fmt yuv420p -f mp4 \"" +
                             output + "\"";
            Execute(command);
        }

        /// <summary>
        /// gif to webm
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void GifToWebmButton_Click(object sender, RoutedEventArgs e)
        {
            if (CheckNotInputFile()) return;
            string output = GetOutputFilepath(".webm");

            // ffmpeg -i input.gif -c vp9  -b:v 0 -crf 20 out3.webm
            string command = "-i \"" + Input + "\" -c vp9  -b:v 0 -crf 40 \"" + output + "\"";
            Execute(command);
        }

        /// <summary>
        /// 视频转GIF
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void VideoToGifButton_Click(object sender, RoutedEventArgs e)
        {
            if (CheckNotInputFile()) return;
            string output = GetOutputFilepath(".gif");

            DateTime start = DateTime.Parse(GvStart.Text);
            DateTime end = DateTime.Parse(GvEnd.Text);
            int t = (end.Second - start.Second);
            if (t < 0)
            {
                MessageBox.Show("时间不能为负!!!");
                return;
            }

            // ffmpeg -ss 30 -t 3 -i input.mp4 -vf "fps=10,scale=320:-1:flags=lanczos,split[s0][s1];[s0]palettegen[p];[s1][p]paletteuse" -loop 0 output.gif
            string command =
                $"-ss {GvStart.Text} -t {t} -i \"{Input}\" -vf \"fps={GvFps.Text},scale={GvW.Text}:{GvH.Text}:flags=lanczos,split[s0][s1];[s0]palettegen[p];[s1][p]paletteuse\" -loop 0 \"{output}\"";
            Execute(command);
        }

        /// <summary>
        /// 视频中提取图片
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void GetVideoFrameButton_Click(object sender, RoutedEventArgs e)
        {
            if (CheckNotInputFile()) return;

            DateTime start = DateTime.Parse(GetImgStart.Text);
            DateTime end = DateTime.Parse(GetImgEnd.Text);
            int t = (end.Second - start.Second);
            if (t < 0)
            {
                MessageBox.Show("时间不能为负!!!");
                return;
            }

            string outDir = Path.Combine(Path.GetDirectoryName(Input) ?? string.Empty, $"[{getFileKey()}]-images");
            Directory.CreateDirectory(outDir);
            string output = Path.Combine(outDir, $"%{GetImgNum.Text}d.jpg");
            // ffmpeg -i 2.mp4 -r 1 -ss 00:00:20 -t 1 -f image2 o-%4d.jpg
            string command = $"-i \"{Input}\" -r {GetImgFps.Text} -ss {GetImgStart.Text} -t {t} -f image2 \"{output}\"";
            Execute(command);
        }

        /// <summary>
        /// 将帧图片合成为视频
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void MergaFrameToVideoButton_Click(object sender, RoutedEventArgs e)
        {
            if (CheckNotInputFile()) return;
            string inputImage = Path.Combine(Path.GetDirectoryName(Input) ?? string.Empty, $"%{GetImgNum2.Text}d.jpg");
            string pout = Path.Combine(Path.GetDirectoryName(Input) ?? string.Empty, $"new-{getFileKey()}.mp4");

            // ffmpeg -i %2d.jpg new.mp4
            string command = $"-i \"{inputImage}\" \"{pout}\"";
            Execute(command);
        }

        /// <summary>
        /// 下载m3u8到mp4
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void M3U8ToMp4Button_Click(object sender, RoutedEventArgs e)
        {
            if (CheckNotInputFile()) return;

            // 选择下载目录
            var dialog = new System.Windows.Forms.FolderBrowserDialog();
            System.Windows.Forms.DialogResult result = dialog.ShowDialog();
            if (result != System.Windows.Forms.DialogResult.OK || dialog.SelectedPath.Length == 0) return;

            // ffmpeg -i http://xxx/index.m3u8 -bsf:a aac_adtstoasc -c copy out.mp4
            string command =
                $"-i \"{Input}\" -bsf:a aac_adtstoasc -c copy \"{Path.Combine(dialog.SelectedPath, $"{getFileKey()}.mp4")}\"";
            Execute(command);
        }

        /// <summary>
        /// 手动输入 输入文件
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void InputFile_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            Input = InputFile.Text;
        }

        /// <summary>
        /// 将输入文件转为MP4格式的视频文件
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ToMp4Button_Click(object sender, RoutedEventArgs e)
        {
            if (CheckNotInputFile()) return;
            string oout = GetOutputFilepath(".mp4");

            // ffmpeg -i output.flv -vcodec libx264 -pix_fmt yuv420p -c:a copy o5.mp4
            string command = $"-i \"{Input}\" -vcodec libx264 -pix_fmt yuv420p -c:a copy \"{oout}\"";
            Execute(command);
        }

        /// <summary>
        /// 转图片格式
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ImageFormatSwitchButton_Click(object sender, RoutedEventArgs e)
        {
            if (CheckNotInputFile()) return;
            string pout = GetOutputFilepath($".{Regex.Replace(ToneWType.Text, @"^\.+", "")}");

            string command = $"-i \"{Input}\" \"{pout}\"";
            Execute(command);
        }

        /// <summary>
        /// 合并两个音频
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void MergaAudioButton_Click(object sender, RoutedEventArgs e)
        {
            if (CheckNotInputFile() || InputAudio.Text.Length == 0) return;
            string pout = GetOutputFilepath(".mp3");
            // ffmpeg.exe -i a1.mp3 -i a2.mp3 -filter_complex amerge -c:a libmp3lame -q:a 4 out.mp3
            string command =
                $"-i \"{Input}\" -i \"{InputAudio.Text}\" -filter_complex amerge -c:a libmp3lame -q:a 4 \"{pout}\"";
            Execute(command);
        }

        /// <summary>
        /// 图片格式批量转换
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void BatchImageFormatSwitchButton_Click(object sender, RoutedEventArgs e)
        {
            if (CheckNotInputFile()) return;
            string newExtension = $".{Regex.Replace(ToneWType.Text, @"^\.+", "")}";

            string dir = Path.GetDirectoryName(Input);
            string extension = Path.GetExtension(Input);
            string newInput = Path.Combine(dir, $"*{extension}");

            string fey = getFileKey();
            string odor = Path.Combine(dir, fey);
            Directory.CreateDirectory(odor);

            // (for %i in (*.jpg) do ffmpeg -i %i %~ni.webp)
            // (for %i in (.\xx\*.jpg) do ffmpeg -i %i %~dpitime\%~ni.webp)
            string command =
                $"/C \"(for %i in (\"{newInput}\") do \"{FfMpegExe}\" -i %i \"%~dpi{fey}\\%~ni{newExtension}\")\"";
            System.Diagnostics.Process.Start("cmd.exe", command);
        }

        /// <summary>
        /// 转换视频帧率
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void VideoFPSButton_Click(object sender, RoutedEventArgs e)
        {
            if (CheckNotInputFile() || ToFps.Text.Length == 0) return;
            string pout = GetOutputFilepath();
            // ffmpeg -i 1.mp4 -vf "setpts=1.0*PTS" -c:a copy -r 30 o.mp4
            string command = $"-i \"{Input}\" -vf \"setpts=1.0*PTS\" -c:a copy -r {ToFps.Text} \"{pout}\"";
            Execute(command);
        }

        /// <summary>
        /// 转换视频的比特率
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void VideoBitRateButton_Click(object sender, RoutedEventArgs e)
        {
            if (CheckNotInputFile() || ToBitRate.Text.Length == 0) return;
            string pout = GetOutputFilepath();
            // ffmpeg -i 1.mp4 -b:v 1M -c:a copy o.mp4
            string command = $"-i \"{Input}\" -b:v {ToBitRate.Text} -c:a copy \"{pout}\"";
            Execute(command);
        }

        /// <summary>
        /// 播放时控制器详情
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void PlayDetailsButton_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show(
                "f 切换全屏\n" +
                "p 却换暂停\n" +
                "m 切换静音\n" +
                "9,0 减少和增加音量\n" +
                "left/right 向后/向前搜索10秒\n" +
                "down/up 向后/向前搜索1分钟\n"
            );
        }
    }
}
