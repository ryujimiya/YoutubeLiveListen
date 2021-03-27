using MyUtilLib;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace YoutubeLiveListen
{
    /// <summary>
    /// MainWindow.xaml の相互作用ロジック
    /// </summary>
    public partial class MainWindow : Window
    {
        /// <summary>
        /// タイトルのベース
        /// </summary>
        private string titleBase = "";
        /// <summary>
        /// 棒読みちゃん
        /// </summary>
        private MyUtilLib.BouyomiChan bouyomiChan = new MyUtilLib.BouyomiChan();

        /// <summary>
        /// ツイキャスクライアント
        /// </summary>
        private YoutubeChatClient YoutubeChatClient;

        /// <summary>
        /// コンストラクタ
        /// </summary>
        public MainWindow()
        {
            InitializeComponent();

            // GUI初期処理
            titleBase = this.Title + " " + MyUtil.GetFileVersion();
            this.Title = titleBase;
        }

        /// <summary>
        /// ウィンドウが開かれた
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            var window = new SettingWindow();
            window.Owner = this;
            window.ShowDialog();
            string apiKey = window.APIKey;

            YoutubeChatClient = new YoutubeChatClient();
            YoutubeChatClient.OnCommentReceiveEach += YoutubeChatClient_OnCommentReceiveEach;
            YoutubeChatClient.OnCommentReceiveDone += YoutubeChatClient_OnCommentReceiveDone;
            YoutubeChatClient.APIKey = apiKey;
        }

        /// <summary>
        /// ウィンドウが閉じられようとしている
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            YoutubeChatClient.Stop();
            bouyomiChan.ClearText();
            bouyomiChan.Dispose();
        }

        /// <summary>
        /// ウィンドウのサイズが変更された
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Window_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            // ウィンドウの高さ Note:最大化のときthis.Heightだと値がセットされない
            double height = this.RenderSize.Height;
            // データグリッドの高さ変更
            stackPanel1.Height = height - SystemParameters.CaptionHeight;
            dataGrid.Height = stackPanel1.Height - wrapPanel1.Height;
        }

        /// <summary>
        /// コメント受信イベントハンドラ
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="comment"></param>
        private void YoutubeChatClient_OnCommentReceiveEach(YoutubeChatClient sender, CommentStruct comment)
        {
            // コメントの追加
            UiCommentData uiCommentData = new UiCommentData();
            uiCommentData.UserThumbUrl = "";
            uiCommentData.UserName = comment.UserName;
            uiCommentData.CommentStr = comment.Text;

            System.Diagnostics.Debug.WriteLine("UserThumbUrl " + uiCommentData.UserThumbUrl);
            System.Diagnostics.Debug.WriteLine("UserName " + uiCommentData.UserName);
            System.Diagnostics.Debug.WriteLine("CommentStr " + uiCommentData.CommentStr);

            ViewModel viewModel = this.DataContext as ViewModel;
            ObservableCollection<UiCommentData> uiCommentDataList = viewModel.UiCommentDataCollection;
            uiCommentDataList.Add(uiCommentData);

            // コメントログを記録
            WriteLog(uiCommentData.UserName, uiCommentData.CommentStr);

            // 棒読みちゃんへ送信
            if (comment.IsBouyomiOn)
            {
                string sendText = comment.Text;
                string bcTitle = YoutubeChatClient.BcTitle;
                if (bcTitle != "")
                {
                    StringBuilder sendTextSb = new StringBuilder(sendText);
                    sendTextSb.Replace("(" + bcTitle + ")", "");
                    sendText = sendTextSb.ToString();
                }
                bouyomiChan.Talk(sendText);
            }
        }

        /// <summary>
        /// コメントログを記録する
        /// </summary>
        /// <param name="userName"></param>
        /// <param name="commentText"></param>
        private void WriteLog(string userName, string commentText)
        {
            string logText = userName + "\t" + commentText;
            System.IO.StreamWriter sw = new System.IO.StreamWriter(
                @"comment.txt",
                true, // append : true
                System.Text.Encoding.GetEncoding("UTF-8"));
            sw.WriteLine(logText);
            sw.Close();
        }

        /// <summary>
        /// コメント受信完了イベントハンドラ
        /// </summary>
        /// <param name="sender"></param>
        private void YoutubeChatClient_OnCommentReceiveDone(YoutubeChatClient sender)
        {
            // データグリッドを自動スクロール
            DataGridScrollToEnd();
        }

        /// <summary>
        /// データグリッドを自動スクロール
        /// </summary>
        private void DataGridScrollToEnd()
        {
            if (dataGrid.Items.Count > 0)
            {
                var border = VisualTreeHelper.GetChild(dataGrid, 0) as Decorator;
                if (border != null)
                {
                    var scroll = border.Child as ScrollViewer;
                    if (scroll != null) scroll.ScrollToEnd();
                }
            }

        }

        /// <summary>
        /// ツイキャスボタンがクリックされた
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void YoutubeBtn_Click(object sender, EventArgs e)
        {
            // 既定ブラウザで放送ページを開く
            if (YoutubeChatClient.BcUrl == "")
            {
                return;
            }
            string url = YoutubeChatClient.BcUrl;

            // ブラウザを開く
            System.Diagnostics.Process.Start(url);
        }

        /// <summary>
        /// 更新ボタンがクリックされた
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void updateBtn_Click(object sender, RoutedEventArgs e)
        {
            DoOpen();
        }

        /// <summary>
        /// ライブIDテキストボックスのキーアップイベントハンドラ
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void channelNameTextBox_KeyUp(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                DoOpen();
            }
        }

        /// <summary>
        /// フォームのタイトルを設定する
        /// </summary>
        private void setTitle()
        {
            // チャンネル名をGUIに設定
            string channelName = YoutubeChatClient.VideoId;
            if (channelName != null && channelName.Length != 0)
            {
                this.Title = channelName + " - " + titleBase;
            }
            else
            {
                this.Title = titleBase;
            }
        }

        /// <summary>
        /// チャンネル情報の初期化
        /// </summary>
        private void initChannelInfo()
        {
            YoutubeChatClient.InitChannelInfo();

            // タイトルを設定
            setTitle();
        }

        /// <summary>
        /// ページを開く
        /// </summary>
        private void DoOpen()
        {

            // タイマーが停止するまで待つ
            YoutubeChatClient.Stop();

            // 初期化
            // チャンネルの初期化
            initChannelInfo();

            // チャット窓の初期化
            InitChatWindow();

            // ビデオIdを取得
            string videoId = GetVideoIdFromGui();
            if (videoId == "")
            {
                return;
            }
            // チャンネル名のセット
            YoutubeChatClient.VideoId = videoId;
            // タイトルを設定
            setTitle();

            // チャットをオープンする
            bool ret = YoutubeChatClient.Start();
            if (!ret)
            {
                // チャンネルの初期化
                initChannelInfo();
                return;
            }

            System.Diagnostics.Debug.WriteLine("doOpen end");
        }

        /// <summary>
        /// チャット窓の初期化
        /// </summary>
        private void InitChatWindow()
        {
            bouyomiChan.ClearText();

            ViewModel viewModel = this.DataContext as ViewModel;
            ObservableCollection<UiCommentData> uiCommentDataList = viewModel.UiCommentDataCollection;
            uiCommentDataList.Clear();
        }

        /// <summary>
        /// チャンネル名をGUIから取得する
        /// </summary>
        /// <returns></returns>
        private string GetVideoIdFromGui()
        {
            return videoIdTextBox.Text;
        }
    }
}
