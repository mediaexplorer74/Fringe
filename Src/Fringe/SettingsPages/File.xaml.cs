using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Storage.Provider;
using Windows.Storage;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;
using System.Diagnostics;
using System.Collections.ObjectModel;
using HtmlAgilityPack;
using Newtonsoft.Json;
using System.Reflection.PortableExecutable;

namespace App3.SettingsPages
{
    public sealed partial class File : Page
    {
        public File()
        {
            this.InitializeComponent();
        }

        private async void Collection_Import(object sender, RoutedEventArgs e)
        {
            Collection_Import_Button.IsEnabled = false;

            Windows.Storage.Pickers.FileOpenPicker OpenPicker = new Windows.Storage.Pickers.FileOpenPicker();
            OpenPicker.SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.Downloads;
            OpenPicker.FileTypeFilter.Add(".html");
            OpenPicker.FileTypeFilter.Add(".json");
            Windows.Storage.StorageFile file = await OpenPicker.PickSingleFileAsync();
            if (file != null)
            {
                if (file.FileType == ".json")
                {
                    using (var stream = await file.OpenStreamForReadAsync())
                    using (var reader = new StreamReader(stream))
                    {
                        string text = await reader.ReadToEndAsync();
                        var importedCollection = JsonConvert.DeserializeObject<ObservableCollection<Collection_List>>(text);
                        if (importedCollection != null)
                        {
                            foreach (var CollectionItem in importedCollection)
                            {
                                (Application.Current as App).CollectionList.Add(CollectionItem);
                            }
                        }
                    }
                }
                else if (file.FileType == ".html")
                {
                    // Create an HTMLDOCCENT object and load the HTML file
                    var htmlDoc = new HtmlDocument();
                    try
                    {
                        htmlDoc.Load(await file.OpenStreamForReadAsync());
                    }
                    catch
                    {
                        return;
                    }

                    // Find all <dt> nodes, because bookmarks and folders are usually included in the <dt> tag
                    var dtNodes = htmlDoc.DocumentNode.SelectNodes("//dt");
                    if (dtNodes != null)
                    {
                        foreach (var dtNode in dtNodes)
                        {
                            // Find the <a> tag, which represents a bookmark
                            var aNode = dtNode.SelectSingleNode("a");
                            if (aNode != null)
                            {
                                string title = aNode.InnerText.Trim();
                                string url = aNode.GetAttributeValue("href", "");
                                if (!string.IsNullOrEmpty(url))
                                {
                                    (Application.Current as App).CollectionList.Add(new Collection_List { CollectionTitle = title, CollectionUri = url });
                                }
                            }
                        }
                    }
                }
            }
            else
            {

            }

            Windows.Storage.StorageFolder StorageFolder = Windows.Storage.ApplicationData.Current.LocalFolder;
            string CollectionJson = JsonConvert.SerializeObject((Application.Current as App).CollectionList);
            try
            {
                Windows.Storage.StorageFile CollectionFile = await StorageFolder.CreateFileAsync("LocalStorage2\\Collections.json",
                    Windows.Storage.CreationCollisionOption.OpenIfExists);
                await Windows.Storage.FileIO.WriteTextAsync(CollectionFile, CollectionJson);
            }
            catch { }

            Collection_Import_Button.IsEnabled = true;
        }

        private async void History_Clear(object sender, RoutedEventArgs e)
        {
            HistoryClearFlyout.Hide();

            (Application.Current as App).HistoryList.Clear();

            string HistoryJson = JsonConvert.SerializeObject((Application.Current as App).HistoryList);
            Windows.Storage.StorageFolder StorageFolder = Windows.Storage.ApplicationData.Current.LocalFolder;
            try
            {
                Windows.Storage.StorageFile HistoryFile = await StorageFolder.CreateFileAsync("LocalStorage2\\History.json", 
                    Windows.Storage.CreationCollisionOption.OpenIfExists);
                await Windows.Storage.FileIO.WriteTextAsync(HistoryFile, HistoryJson);
            }
            catch { }
        }

        private async void Collection_ExportToHtml(object sender, RoutedEventArgs e)
        {
            Collection_Export_Button.IsEnabled = false;

            Windows.Storage.StorageFolder StorageFolder = Windows.Storage.ApplicationData.Current.LocalFolder;
            Windows.Storage.StorageFile CollectionFile;

            // Create an HTML document
            var htmlDoc = new HtmlDocument();
            htmlDoc.LoadHtml(@"<!DOCTYPE html><html><head><body></body></head></html>"); // Initialize the basic structure

            try
            {
                // Get the header, add metadata and title
                var headNode = htmlDoc.DocumentNode.SelectSingleNode("//head");

                var metaNode = new HtmlNode(HtmlNodeType.Element, headNode.OwnerDocument, 0);
                metaNode.Name = "meta";
                metaNode.Attributes.Add("charset", "UTF-8");
                headNode.AppendChild(metaNode);

                // Create main content
                var bodyNode = htmlDoc.DocumentNode.SelectSingleNode("//body");
                var mainHeading = HtmlNode.CreateNode("<h1>收藏夹</h1>");
                bodyNode.AppendChild(mainHeading);

                // Create an unordered list
                var listNode = HtmlNode.CreateNode("<dl></dl>");
                bodyNode.AppendChild(listNode);

                // Traverse the data to generate list items
                foreach (var item in (Application.Current as App).CollectionList)
                {
                    var listItem = HtmlNode.CreateNode("<dt></dt>");
                    var link = HtmlNode.CreateNode($"<a href=\"{(item.CollectionUri)}\">{(item.CollectionTitle)}</a>");
                    listItem.AppendChild(link);
                    listNode.AppendChild(listItem);
                }
            }
            catch { }

            Windows.Storage.Pickers.FileSavePicker SavePicker = new Windows.Storage.Pickers.FileSavePicker();
            SavePicker.SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.Downloads;
            SavePicker.FileTypeChoices.Add("HTML", new List<string>() { ".html" });
            SavePicker.DefaultFileExtension = ".html";
            SavePicker.SuggestedFileName = "FringeCollections " + DateTime.Now;
            CollectionFile = await SavePicker.PickSaveFileAsync();
            if (CollectionFile != null)
            {
                using (var stream = await CollectionFile.OpenStreamForWriteAsync())
                {
                    using (var tw = new StreamWriter(stream))
                    {
                        tw.Write(htmlDoc.DocumentNode.OuterHtml);
                        stream.SetLength(stream.Position);
                    }
                }
                FileUpdateStatus status = await CachedFileManager.CompleteUpdatesAsync(CollectionFile);

                if (status == Windows.Storage.Provider.FileUpdateStatus.Complete)
                {

                }
                else
                {

                }
            }
            else
            {

            }

            Collection_Export_Button.IsEnabled = true;
        }

        private async void Collection_ExportToJson(object sender, RoutedEventArgs e)
        {
            Collection_Export_Button.IsEnabled = false;

            Windows.Storage.StorageFolder StorageFolder = Windows.Storage.ApplicationData.Current.LocalFolder;
            Windows.Storage.StorageFile CollectionFile;
            string CollectionJson = JsonConvert.SerializeObject((Application.Current as App).CollectionList);

            Windows.Storage.Pickers.FileSavePicker SavePicker = new Windows.Storage.Pickers.FileSavePicker();
            SavePicker.SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.Downloads;
            SavePicker.FileTypeChoices.Add("JSON", new List<string>() { ".json" });
            SavePicker.DefaultFileExtension = ".json";
            SavePicker.SuggestedFileName = "FringeCollections " + DateTime.Now;
            CollectionFile = await SavePicker.PickSaveFileAsync();
            if (CollectionFile != null)
            {
                using (var stream = await CollectionFile.OpenStreamForWriteAsync())
                {
                    using (var tw = new StreamWriter(stream))
                    {
                        tw.Write(CollectionJson);
                        stream.SetLength(stream.Position);
                    }
                }
                FileUpdateStatus status = await CachedFileManager.CompleteUpdatesAsync(CollectionFile);

                if (status == Windows.Storage.Provider.FileUpdateStatus.Complete)
                {

                }
                else
                {

                }
            }
            else
            {

            }

            Collection_Export_Button.IsEnabled = true;
        }

        private async void History_Export_Button_Click(object sender, RoutedEventArgs e)
        {
            History_Export_Button.IsEnabled = false;

            Windows.Storage.StorageFolder StorageFolder = Windows.Storage.ApplicationData.Current.LocalFolder;
            Windows.Storage.StorageFile HistoryFile;
            string HistoryJson = JsonConvert.SerializeObject((Application.Current as App).HistoryList);

            Windows.Storage.Pickers.FileSavePicker SavePicker = new Windows.Storage.Pickers.FileSavePicker();
            SavePicker.SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.Downloads;
            SavePicker.FileTypeChoices.Add("JSON", new List<string>() { ".json" });
            SavePicker.DefaultFileExtension = ".json";
            SavePicker.SuggestedFileName = "FringeHistory " + DateTime.Now;
            HistoryFile = await SavePicker.PickSaveFileAsync();
            if (HistoryFile != null)
            {
                using (var stream = await HistoryFile.OpenStreamForWriteAsync())
                {
                    using (var tw = new StreamWriter(stream))
                    {
                        tw.Write(HistoryJson);
                    }
                }
                FileUpdateStatus status = await CachedFileManager.CompleteUpdatesAsync(HistoryFile);

                if (status == Windows.Storage.Provider.FileUpdateStatus.Complete)
                {

                }
                else
                {

                }
            }
            else
            {

            }

            History_Export_Button.IsEnabled = true;
        }

        private async void History_Import_Button_Click(object sender, RoutedEventArgs e)
        {
            History_Import_Button.IsEnabled = false;

            Windows.Storage.Pickers.FileOpenPicker OpenPicker = new Windows.Storage.Pickers.FileOpenPicker();
            OpenPicker.SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.Downloads;
            OpenPicker.FileTypeFilter.Add(".json");
            Windows.Storage.StorageFile file = await OpenPicker.PickSingleFileAsync();
            if (file != null)
            {
                using (var stream = await file.OpenStreamForReadAsync())
                using (var reader = new StreamReader(stream))
                {
                    string text = await reader.ReadToEndAsync();
                    var importedHistory = JsonConvert.DeserializeObject<ObservableCollection<History_List>>(text);
                    if (importedHistory != null)
                    {
                        int i = 0;
                        foreach (var historyItem in importedHistory)
                        {
                            (Application.Current as App).HistoryList.Insert(i, historyItem);
                            i++;
                        }
                    }
                }
            }

            try
            {
                string HistoryJson = JsonConvert.SerializeObject((Application.Current as App).HistoryList);
                Windows.Storage.StorageFolder StorageFolder = Windows.Storage.ApplicationData.Current.LocalFolder;
                Windows.Storage.StorageFile HistoryFile = await StorageFolder.CreateFileAsync("LocalStorage2\\History.json", Windows.Storage.CreationCollisionOption.OpenIfExists);
                await Windows.Storage.FileIO.WriteTextAsync(HistoryFile, HistoryJson);
            }
            catch { }

            History_Import_Button.IsEnabled = true;
        }
    }
}
