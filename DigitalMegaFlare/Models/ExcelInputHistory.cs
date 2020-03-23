﻿using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace DigitalMegaFlare.Models
{
	/// <summary>
	/// Excel読み込み履歴です。
	/// </summary>
	public class ExcelInputHistory
	{
		/// <summary>
		/// IDを取得、もしくは、設定します。
		/// </summary>
		[Key]
		public long Id { get; set; }

		/// <summary>
		/// ロック状態を取得、もしくは、設定します。
		/// </summary>
		public bool IsLocked { get; set; }

		/// <summary>
		/// 元ファイル名を取得、もしくは、設定します。
		/// </summary>
		public string RawFileName { get; set; }

		/// <summary>
		/// サーバ上ファイル名を取得、もしくは、設定します。
		/// </summary>
		public string FileName { get; set; }

		/// <summary>
		/// コメントを取得、もしくは、設定します。
		/// </summary>
		public string Comment { get; set; }

		/// <summary>
		/// 取込日時を取得、もしくは、設定します。
		/// </summary>
		public DateTime InputDate { get; set; }

		/// <summary>
		/// アップロード者のリモートホスト情報を取得、もしくは、設定します。
		/// </summary>
		public string Host { get; set; }

		/// <summary>
		/// アップロード者のIPアドレスを取得、もしくは、設定します。
		/// </summary>
		public string Ip { get; set; }

		/// <summary>
		/// アップロード者のリクエスト時URLを取得、もしくは、設定します。
		/// </summary>
		public string Url { get; set; }

		///// <summary>
		///// Excelのバイナリデータを取得、もしくは、設定します。
		///// </summary>
		//public byte[] Xlsx { get; set; }

		//// アップロードされたファイル（List<IFormFile> uploadFiles）
		//using (var memoryStream = new MemoryStream())
		//{
		//    await uploadFiles[i].CopyToAsync(memoryStream);
		//    document.Xlsx = memoryStream.ToArray();
		//}

		//// 任意のファイル
		//using (var fs = new FileStream(@"C:\test.txt", System.IO.FileMode.Open, System.IO.FileAccess.Read))
		//{
		//    //ファイルを読み込むバイト型配列を作成する
		//    byte[] bs = new byte[fs.Length];
		//    //ファイルの内容をすべて読み込む
		//    fs.Read(bs, 0, bs.Length);
		//    //閉じる
		//    fs.Close();
		//}

		//return File(history.Xlsx, "application/pdf", $"stampHistory{stampHistoryId}.pdf");

		public override string ToString()
		{
			return $"ID：{Id}, ファイル名：{FileName}, 取込日時：{InputDate}, ホスト：{Host}, IPアドレス：{Ip}, リクエスト時URL：{Url}";
		}
	}
}
