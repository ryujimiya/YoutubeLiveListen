using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Threading; // DispatcherTimer
using System.Net; // WebClient
using System.Text.RegularExpressions; // Regex
using System.IO; // StreamReader
using HtmlAgilityPack;
using Newtonsoft.Json;
using MyUtilLib;

namespace YoutubeLiveListen
{    /// <summary>
     /// コメント受信イベントハンドラデリゲート
     /// </summary>
     /// <param name="sender">チャットクライアント</param>
     /// <param name="comment">受信したコメント構造体</param>
    public delegate void OnCommentReceiveEachDelegate(YoutubeChatClient sender, CommentStruct comment);
    /// <summary>
    /// コメント受信完了イベントハンドラデリゲート
    /// <param name="sender">チャットクライアント</param>
    /// </summary>
    public delegate void OnCommentReceiveDoneDelegate(YoutubeChatClient sender);
    /// <summary>
    /// 動画IDが変更されたときのイベントハンドラデリゲート
    /// </summary>
    /// <param name="sender">チャットクライアント</param>
    public delegate void OnMovieIdChangedDelegate(YoutubeChatClient sender);

    /// <summary>
    /// コメント構造体
    /// </summary>
    public struct CommentStruct
    {
        /// <summary>
        /// コメントテキスト
        /// </summary>
        public string Text;
        /// <summary>
        /// ユーザー名
        /// </summary>
        public string UserName;
        /// <summary>
        /// 棒読みちゃんの音を出す？
        /// </summary>
        public bool IsBouyomiOn;
    }

    /// <summary>
    /// Youtubeのチャットクライアント（機能はコメント受信のみ)
    /// </summary>
    public class YoutubeChatClient
    {
        ///////////////////////////////////////////////////////////////////////
        //定数
        ///////////////////////////////////////////////////////////////////////
        public string APIKey { get; set; } = "";

        ///////////////////////////////////////////////////////////////////////
        // 型
        ///////////////////////////////////////////////////////////////////////
        public class VideosLiveStreamingDetails
        {
            public DateTime actualStartTime { get; set; }
            public DateTime scheduledStartTime { get; set; }
            public string concurrentViewers { get; set; }
            public string activeLiveChatId { get; set; }
        }

        public class VideosItem
        {
            public string kind { get; set; }
            public string etag { get; set; }
            public string id { get; set; }
            public VideosLiveStreamingDetails liveStreamingDetails { get; set; }
        }

        public class VideosPageInfo
        {
            public int totalResults { get; set; }
            public int resultsPerPage { get; set; }
        }

        public class VideosRoot
        {
            public string kind { get; set; }
            public string etag { get; set; }
            public List<VideosItem> items { get; set; }
            public VideosPageInfo pageInfo { get; set; }
        }

        /////////////////
        public class PageInfo
        {
            public int totalResults { get; set; }
            public int resultsPerPage { get; set; }
        }

        public class TextMessageDetails
        {
            public string messageText { get; set; }
        }

        public class Snippet
        {
            public string type { get; set; }
            public string liveChatId { get; set; }
            public string authorChannelId { get; set; }
            public DateTime publishedAt { get; set; }
            public bool hasDisplayContent { get; set; }
            public string displayMessage { get; set; }
            public TextMessageDetails textMessageDetails { get; set; }
        }

        public class AuthorDetails
        {
            public string channelId { get; set; }
            public string channelUrl { get; set; }
            public string displayName { get; set; }
            public string profileImageUrl { get; set; }
            public bool isVerified { get; set; }
            public bool isChatOwner { get; set; }
            public bool isChatSponsor { get; set; }
            public bool isChatModerator { get; set; }
        }

        public class Item
        {
            public string kind { get; set; }
            public string etag { get; set; }
            public string id { get; set; }
            public Snippet snippet { get; set; }
            public AuthorDetails authorDetails { get; set; }
        }

        public class Root
        {
            public string kind { get; set; }
            public string etag { get; set; }
            public int pollingIntervalMillis { get; set; }
            public PageInfo pageInfo { get; set; }
            public string nextPageToken { get; set; }
            public List<Item> items { get; set; }
        }

        ///////////////////////////////////////////////////////////////////////
        // 定数
        ///////////////////////////////////////////////////////////////////////
        /// <summary>
        /// 最大保持コメント数
        /// </summary>
        private const int MaxStoredCommentCnt = 40;

        ///////////////////////////////////////////////////////////////////////
        // フィールド
        ///////////////////////////////////////////////////////////////////////
        /// <summary>
        /// 動画ID
        /// </summary>
        public string VideoId { get; set; }
        /// <summary>
        /// チャットID
        /// </summary>
        public string ChatId { get; private set; }
        /// <summary>
        /// 放送URL
        /// </summary>
        public string BcUrl { get; private set; }
        /// <summary>
        /// コメントリスト
        /// </summary>
        public IList<CommentStruct> CommentList { get; private set; }
        // 次のページのトークン
        private string NextPageToken = "";
        /// <summary>
        /// 放送タイトル
        /// </summary>
        public string BcTitle { get; private set; }

        /// <summary>
        /// コメント受信イベントハンドラ
        /// </summary>
        public event OnCommentReceiveEachDelegate OnCommentReceiveEach = null;
        /// <summary>
        /// コメント受信完了イベントハンドラ
        /// </summary>
        public event OnCommentReceiveDoneDelegate OnCommentReceiveDone = null;
       /// <summary>
        /// コメント取得タイマー
        /// </summary>
        private DispatcherTimer MainDTimer;
        /// <summary>
        /// タイマー処理実行中？
        /// </summary>
        private bool IsTimerProcess = false;

        private bool IsFirst = false;

        /// <summary>
        /// コンストラクタ
        /// </summary>
        public YoutubeChatClient()
        {
            CommentList = new List<CommentStruct>();

            MainDTimer = new DispatcherTimer(DispatcherPriority.Normal);
            MainDTimer.Interval = new TimeSpan(0, 0, 5);
            MainDTimer.Tick += new EventHandler(MainDTimer_Tick);

            InitChannelInfo();
        }

        /// <summary>
        /// チャンネル情報の初期化
        /// </summary>
        public void InitChannelInfo()
        {
            VideoId = "";
            BcUrl = "";
            ChatId = "";
            CommentList.Clear();
        }

        /// <summary>
        /// コメント受信処理を開始する
        /// </summary>
        public bool Start()
        {
            // 放送URLを取得
            if (VideoId == "")
            {
                return false;
            }

            ChatId = "";
            IsFirst = true;

            BcUrl = "https://www.youtube.com/watch?v=" + VideoId;
            GetChatId();

            // メインタイマー処理
            DoMainTimerProc();

            // メインタイマーを開始
            MainDTimer.Start();

            return true;
        }

        private void GetChatId()
        {
            ChatId = "";
            string url = "https://www.googleapis.com/youtube/v3/videos?" +
                "key=" + APIKey +
                "&id=" + VideoId +
                "&part=" + "liveStreamingDetails";
            string recvStr = DoHttpRequest(url);
            //System.Diagnostics.Debug.WriteLine("recvStr:[" + recvStr + "]");
            if (recvStr == null)
            {
                // 接続エラー
                return;
            }
            try
            {
                // JSON形式からコメント応答オブジェクトに変換
                VideosRoot videos = JsonConvert.DeserializeObject<VideosRoot>(recvStr);
                var liveStreamingDetails = videos.items[0].liveStreamingDetails;
                ChatId = liveStreamingDetails.activeLiveChatId;
            }
            catch (Exception exception)
            {
                System.Diagnostics.Debug.WriteLine(exception.Message + " " + exception.StackTrace);
                System.Diagnostics.Debug.WriteLine("[Error]GetChatId: recvStr: " + recvStr);
                return;
            }
        }

        /// <summary>
        /// コメント受信処理を停止する>
        ///   タイマーが停止するまで待つ
        /// </summary>
        public void Stop()
        {
            // タイマーを停止する
            MainDTimer.Stop();
        }

        /// <summary>
        /// タイマーイベントハンドラ
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void MainDTimer_Tick(object sender, EventArgs e)
        {
            if (IsTimerProcess)
            {
                return;
            }

            IsTimerProcess = true;
            DoMainTimerProc();
            IsTimerProcess = false;
        }

        /// <summary>
        /// メインタイマー処理
        /// </summary>
        private void DoMainTimerProc()
        {
            IList<CommentStruct> workCommentList = null;
            // コメント更新一覧を取得する
            workCommentList = GetBcCmntListUpdate();
            if (workCommentList == null)
            {
                return;
            }

            // コメントをGUIに登録する
            SetCmntToGui(workCommentList);
        }

        /// <summary>
        /// コメントをGUIに登録する
        /// </summary>
        /// <param name="workCommentList"></param>
        private void SetCmntToGui(IList<CommentStruct> workCommentList)
        {
            // 登録済みの最新コメントを取得
            for (int iComment = 0; iComment < workCommentList.Count; iComment++)
            {
                CommentStruct tagtComment = workCommentList[iComment];

                // 新規のコメントの場合、リストに追加する
                CommentList.Add(tagtComment);
                System.Diagnostics.Debug.WriteLine("■{0} {1}", tagtComment.UserName, tagtComment.Text);

                // 最大コメント数チェック
                if (CommentList.Count > MaxStoredCommentCnt)
                {
                    CommentList.RemoveAt(0);
                    System.Diagnostics.Debug.Assert(CommentList.Count == MaxStoredCommentCnt);
                }

                if (OnCommentReceiveEach != null)
                {
                    OnCommentReceiveEach(this, tagtComment);
                }
            }

            if (OnCommentReceiveDone != null)
            {
                OnCommentReceiveDone(this);
            }
        }

        /// <summary>
        /// 放送の動画情報を取得可能か？
        /// </summary>
        /// <returns></returns>
        private bool IsBcMovieValid()
        {
            if (VideoId == "")
            {
                return false;
            }
            if (ChatId == "")
            {
                return false;
            }
            return true;
        }

        /// <summary>
        /// コメント更新一覧を取得する
        /// </summary>
        /// <returns></returns>
        private IList<CommentStruct> GetBcCmntListUpdate()
        {
            bool isFirst = IsFirst;
            IsFirst = false;
            IList<CommentStruct> workCommentList = new List<CommentStruct>();

            if (!IsBcMovieValid())
            {
                return workCommentList;
            }

            string url = "https://www.googleapis.com/youtube/v3/liveChat/messages?" +
                "key=" + APIKey +
                "&liveChatId=" + ChatId +
                "&part=" + "id,snippet,authorDetails";
            if (!isFirst)
            {
                url += "&pageToken=" + NextPageToken;
            }
            string recvStr = DoHttpRequest(url);
            if (recvStr == null)
            {
                // 接続エラー
                return workCommentList;
            }
            try
            {
                // JSON形式からコメント応答オブジェクトに変換
                Root bcCmntResponse = JsonConvert.DeserializeObject<Root>(recvStr);
                NextPageToken = bcCmntResponse.nextPageToken;
                var items = bcCmntResponse.items;

                // コメント応答リストからコメントを取り出す
                workCommentList = new List<CommentStruct>();

                bool isBouyomiOn = !isFirst;
                foreach (var item in items)
                {
                    string channelId = item.snippet.authorChannelId;
                    string msg = item.snippet.displayMessage;
                    string usr = item.authorDetails.displayName;

                    var cmnt = new CommentStruct();
                    cmnt.UserName = usr;
                    cmnt.Text = msg;
                    cmnt.IsBouyomiOn = isBouyomiOn;
                    workCommentList.Add(cmnt);
                }
            }
            catch (Exception exception)
            {
                System.Diagnostics.Debug.WriteLine(exception.Message + " " + exception.StackTrace);
                System.Diagnostics.Debug.WriteLine("[Error]getBcCmntListUpdate: recvStr: " + recvStr);
                return workCommentList;
            }
            return workCommentList;
        }

        /// <summary>
        /// HTTPリクエストを送信する
        /// </summary>
        /// <param name="url"></param>
        /// <returns>null:接続エラー または、recvStr:受信文字列</returns>
        private static string DoHttpRequest(string url)
        {
            string recvStr = null;
            using (WebClient webClient = new WebClient())
            {
                Stream stream = null;
                try
                {
                    stream = webClient.OpenRead(url);
                }
                catch (Exception exception)
                {
                    // 接続エラー
                    System.Diagnostics.Debug.WriteLine(exception.Message + " " + exception.StackTrace);
                    return recvStr;
                }
                StreamReader sr = new StreamReader(stream);
                recvStr = sr.ReadToEnd();
            }
            return recvStr;
        }

    }
}
