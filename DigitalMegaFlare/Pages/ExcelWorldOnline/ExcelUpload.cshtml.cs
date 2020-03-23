﻿using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ClosedXML.Excel;
using DigitalMegaFlare.Data;
using DigitalMegaFlare.Models;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Mintea.RazorHelper;
using RazorLight;

namespace DigitalMegaFlare.Pages.ExcelWorldOnline
{

    public class ExcelUploadModel : PageModel
    {
        /// <summary>
        /// パス取得に使用する
        /// </summary>
        private readonly IWebHostEnvironment _hostEnvironment = null;
        private readonly ApplicationDbContext _db;

        private readonly IMediator _mediator = null;
        public ExcelUploadModel(IWebHostEnvironment hostEnvironment, IMediator mediator, ApplicationDbContext db)
        {
            _hostEnvironment = hostEnvironment;
            _mediator = mediator;
            _db = db;
        }
        public ExcelUploadResult Data { get; private set; }
        public ExcelListResult History { get; private set; }


        public async Task<IActionResult> OnGetAsync()
        {
            if (Data == null)
            {
                Data = await _mediator.Send(new ExcelUploadQuery());
            }
            // 一覧
            History = await _mediator.Send(new ExcelListQuery());
            return Page();
        }

        #region ロック、ダウンロード、詳細ボタン
        /// <summary>
        /// ロックボタン
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        public async Task<IActionResult> OnPostLockAsync(long id)
        {
            // DBに登録する
            var data = _db.ExcelInputHistories.First(x => x.Id == id);
            data.IsLocked = !data.IsLocked;
            _db.ExcelInputHistories.Update(data);
            await _db.SaveChangesAsync();

            // 再検索
            return await OnGetAsync();
        }

        /// <summary>
        /// ダウンロードボタン
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        public IActionResult OnPostDownload(long id)
        {
            var data = _db.ExcelInputHistories.First(x => x.Id == id);
            var fullpath = Path.Combine(_hostEnvironment.WebRootPath, SystemConstants.FileDirectory, SystemConstants.UploadedExcelsDirectory, data.FileName);
            return File(new FileStream(fullpath, FileMode.Open), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", data.RawFileName);
        }

        /// <summary>
        /// 詳細ボタン
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        public async Task<IActionResult> OnPostDetailAsync(long id)
        {
            var data = _db.ExcelInputHistories.First(x => x.Id == id);
            var fullpath = Path.Combine(_hostEnvironment.WebRootPath, SystemConstants.FileDirectory, SystemConstants.UploadedExcelsDirectory, data.FileName);

            Data = await _mediator.Send(new ExcelUploadQuery { FilePath = fullpath });
            return await OnGetAsync();
        }
        #endregion

        #region 削除ボタン
        /// <summary>
        /// 削除ボタン
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        public async Task<IActionResult> OnPostDeleteAsync(long id)
        {
            var data = _db.ExcelInputHistories.First(x => x.Id == id);
            var fullpath = Path.Combine(_hostEnvironment.WebRootPath, SystemConstants.FileDirectory, SystemConstants.UploadedExcelsDirectory, data.FileName);
            
            // 削除じゃなくてバックアップにする
            var backupFilePath = Path.Combine(_hostEnvironment.WebRootPath, SystemConstants.BackupFileDirectory, "excels", data.FileName);
            System.IO.File.Move(fullpath, backupFilePath);

            // DBからレコードを削除
            _db.ExcelInputHistories.Remove(data);
            await _db.SaveChangesAsync();

            // 再検索
            return await OnGetAsync();
        }
        #endregion

        #region 生成ボタン
        /// <summary>
        /// 生成ボタン
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        public async Task<IActionResult> OnPostGenerateAsync(long id)
        {
            var data = _db.ExcelInputHistories.First(x => x.Id == id);
            var fullpath = Path.Combine(_hostEnvironment.WebRootPath, SystemConstants.FileDirectory, SystemConstants.UploadedExcelsDirectory, data.FileName);
            string excelName = data.FileName;

            // Excelから読み込み
            var excelDirectry = Path.Combine(_hostEnvironment.WebRootPath, SystemConstants.FileDirectory, SystemConstants.UploadedExcelsDirectory);
            var excel = RazorHelper.ReadExcel(excelDirectry, excelName, true);

            // エンジンと一時出力先を作成
            var engine = new RazorLightEngineBuilder()
                          .UseEmbeddedResourcesProject(typeof(Program))
                          .UseMemoryCachingProvider()
                          .DisableEncoding()
                          .Build();
            string outPath = Path.Combine(_hostEnvironment.WebRootPath, "temp");
            RazorHelper.SafeCreateDirectory(outPath);

            // 一時ファイル消す
            DirectoryInfo target = new DirectoryInfo(outPath);
            foreach (FileInfo file in target.GetFiles())
            {
                file.Delete();
            }

            // Modelの作成
            dynamic model;
            try
            {
                model = RazorHelper.CreateModel(excel);
            }
            catch (Exception e)
            {
                ViewData["Error"] = e.Message;
                return Page();
            }

            // リストを読んでソース生成する
            // ↓この"RootList"は動的に変えられないので、ファイル生成の一覧となるListシートの名前は"RootList"固定にする。
            var outFileList = new List<string>();
            var razorFileDirectry = Path.Combine(_hostEnvironment.WebRootPath, SystemConstants.FileDirectory, "razors");
            for (int i = 0; i < model.RootList.Count; i++)
            {
                // 変数入れる
                model.Settings.Index = i.ToString();

                // テンプレート読み込み
                // ファイルアクセス処理
                var template = System.IO.File.ReadAllText(Path.Combine(razorFileDirectry, model.RootList[i].RazorTemplate));

                // ソース生成
                // 同じキーを指定すると登録したスクリプトを使いまわすことが出来るが、何故か2回目以降Unicodeにされるので毎回違うキーを使う。
                var result = await engine.CompileRenderStringAsync($"{model.RootList[i].Name}", template, model);

                // ファイル名生成
                var resultFilename = await engine.CompileRenderStringAsync($"{model.RootList[i].Name}Name", model.RootList[i].OutputFileName, new { model.RootList[i].Name });

                // 生成したファイルを一時保存（今回はやっつけで。本当は人によって一時フォルダ名変えるべき。）
                // VisualStudioが勘違いを起こすのでファイル末尾に"_"をつける
                var outFileName = $"{resultFilename}_";
                outFileList.Add(outFileName);
                System.IO.File.WriteAllText(Path.Combine(outPath, outFileName), result, System.Text.Encoding.UTF8);

                ViewData["Message"] = result;
            }

            // 圧縮ファイルの準備
            string dateFormat = "yyyyMMddHHmmss";
            string outFilePath = Path.Combine(outPath, $"{DateTime.UtcNow.ToString(dateFormat)}.zip");
            // 一時保存したファイルをZipにする
            using (ZipArchive archive = ZipFile.Open(outFilePath, ZipArchiveMode.Create))
            {
                foreach (var item in outFileList)
                {
                    archive.CreateEntryFromFile(
                        Path.Combine(outPath, $"{item}"),
                        $"{item.TrimEnd('_')}",
                        //$"{excelName}/item.TrimEnd('_')}", // ディレクトリ分けする場合はこう書く
                        CompressionLevel.NoCompression
                        );
                }
            }

            return File(new FileStream(outFilePath, FileMode.Open), "application/zip", $"{data.RawFileName}.zip");
        }
        #endregion

        #region Uploadボタン
        /// <summary>
        /// Excelアップロードボタン
        /// </summary>
        /// <param name="file"></param>
        /// <returns></returns>
        public async Task<IActionResult> OnPostUploadAsync(IFormFile file, string comment)
        {
            // アップロードされたファイルをサーバに保存する
            using (var fileStream = file.OpenReadStream())
            {
                var ipAddress = Request.HttpContext.Connection.RemoteIpAddress;
                var hostName = System.Net.Dns.GetHostEntry(ipAddress).HostName;
                var url = Microsoft.AspNetCore.Http.Extensions.UriHelper.GetDisplayUrl(Request);

                var fileDirectry = Path.Combine(_hostEnvironment.WebRootPath, SystemConstants.FileDirectory, SystemConstants.UploadedExcelsDirectory);

                using (PhysicalFileProvider provider = new PhysicalFileProvider(fileDirectry))
                {
                    // ファイル情報を取得
                    var serverFileName = GetUniqueName() + ".xlsx";
                    IFileInfo fileInfo = provider.GetFileInfo(serverFileName);   // ファイル情報

                    // 指定したパスに保存する
                    using (var stream = new FileStream(fileInfo.PhysicalPath, FileMode.Create))
                    {
                        await file.CopyToAsync(stream);

                        // DBに登録する
                        var data = new ExcelInputHistory {
                            RawFileName = file.FileName,
                            Comment = comment,
                            FileName = serverFileName,
                            Ip = ipAddress.ToString(),
                            Host = hostName,
                            InputDate = DateTime.UtcNow,
                            IsLocked = false,
                            Url = url
                        };
                        _db.ExcelInputHistories.Add(data);
                        await _db.SaveChangesAsync();
                    }
                }
            }

            return await OnGetAsync();
        }

        /// <summary>
        /// 8桁のランダムで重複のない文字列を取得する
        /// </summary>
        /// <returns></returns>
        private string GetUniqueName()
        {
            return Path.GetRandomFileName().Split(".")[0];
        }
        #endregion
    }

    #region History

    /// <summary>検索条件</summary>
    public class ExcelListQuery : IRequest<ExcelListResult>
    {
        // 何もなし
    }

    /// <summary>検索結果</summary>
    public class ExcelListResult
    {
        /// <summary>検索した情報</summary> 
        public ExcelInputHistory[] Histories { get; set; }
    }

    /// <summary> 
    /// 検索ハンドラ 
    /// QueryをSendすると動作し、Resultを返す 
    /// </summary> 
    public class ExcelListQueryHandler : IRequestHandler<ExcelListQuery, ExcelListResult>
    {
        private readonly ApplicationDbContext _db;

        public ExcelListQueryHandler(ApplicationDbContext db)
        {
            _db = db;
        }

        /// <summary>
        /// 検索の方法を定義する
        /// IRequestHandlerで実装することになっている
        /// </summary>
        /// <param name="query">検索条件</param>
        /// <param name="token"></param>
        /// <returns></returns>
        public async Task<ExcelListResult> Handle(ExcelListQuery query, CancellationToken token)
        {
            // DB検索
            var histories = _db.ExcelInputHistories.ToArray();

            // 検索結果の格納
            var result = new ExcelListResult
            {
                Histories = histories
            };
            return await Task.FromResult(result);
        }
    }
    #endregion

    #region Upload
    /// <summary>検索条件</summary>
    public class ExcelUploadQuery : IRequest<ExcelUploadResult>
    {
        /// <summary>ファイルパス</summary> 
        public string FilePath { get; set; }
    }

    /// <summary>検索結果</summary>
    public class ExcelUploadResult
    {
        /// <summary>シート名</summary> 
        public List<string> SheetNames { get; set; }

        /// <summary>Excelの内容</summary> 
        public List<List<List<string>>> RawExcel { get; set; }
    }

    /// <summary> 
    /// 検索ハンドラ 
    /// QueryをSendすると動作し、Resultを返す 
    /// </summary> 
    public class ExcelUploadQueryHandler : IRequestHandler<ExcelUploadQuery, ExcelUploadResult>
    {
        /// <summary>
        /// パス取得に使用する
        /// </summary>
        private readonly IWebHostEnvironment _hostEnvironment = null;
        public ExcelUploadQueryHandler(IWebHostEnvironment hostEnvironment)
        {
            _hostEnvironment = hostEnvironment;
        }

        /// <summary>
        /// 検索の方法を定義する
        /// IRequestHandlerで実装することになっている
        /// </summary>
        /// <param name="query">検索条件</param>
        /// <param name="token"></param>
        /// <returns></returns>
        public async Task<ExcelUploadResult> Handle(ExcelUploadQuery query, CancellationToken token)
        {
            // ファイルの読み込み
            // 検索結果の格納
            var result = ReadExcel(query.FilePath);
            return await Task.FromResult(result);
        }

        /// <summary>
        /// Excelを読み込む
        /// </summary>
        /// <param name="filePath">ディレクトリ</param>
        /// <returns></returns>
        private ExcelUploadResult ReadExcel(string filePath)
        {
            // ファイルの読み込み
            List<string> sheetNames = new List<string>();
            List<List<List<string>>> xlsx = new List<List<List<string>>>();
            
            if (!string.IsNullOrWhiteSpace(filePath))
            {
                using (var stream = new FileStream(filePath, FileMode.Open))
                using (var wb = new XLWorkbook(stream))
                {
                    foreach (var ws in wb.Worksheets)
                    {
                        // ワークシート
                        List<List<string>> sheet = new List<List<string>>();

                        // シート名を取得
                        sheetNames.Add(ws.Name);

                        //"行数:" + ws.LastCellUsed().Address.RowNumber.ToString()
                        //"列数:" + ws.LastCellUsed().Address.ColumnNumber.ToString()
                        //"列記号:" + ws.LastCellUsed().Address.ColumnLetter.ToString()

                        for (int i = 1; i <= ws.LastCellUsed().Address.RowNumber; i++)
                        {
                            List<string> raw = new List<string>();
                            for (int j = 1; j <= ws.LastCellUsed().Address.ColumnNumber; j++)
                            {
                                raw.Add(ws.Cell(i, j).Value.ToString());
                            }
                            sheet.Add(raw);
                        }

                        xlsx.Add(sheet);
                    }
                }
            }

            return new ExcelUploadResult
            {
                RawExcel = xlsx,
                SheetNames = sheetNames
            };
        }

        #region Excelファイル作成（使ってない）
        ///// <summary>
        ///// Excelファイル作成
        ///// </summary>
        ///// <param name="id"></param>
        ///// <returns></returns>
        //private async Task<XLWorkbook> BuildExcelFile(int id)
        //{
        //    var t = Task.Run(() =>
        //    {
        //        // ブック作成
        //        var wb = new XLWorkbook();
        //        // シート作成
        //        var ws = wb.AddWorksheet("Sheet1");
        //        // 最初のセルに値を設定
        //        ws.FirstCell().SetValue(id);
        //        // 保存
        //        //wb.SaveAs("HelloWorld.xlsx");
        //        return wb;
        //    });
        //    return await t;
        //}

        #endregion

    }
    #endregion

}