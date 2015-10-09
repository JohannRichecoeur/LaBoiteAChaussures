using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Storage;
using Windows.Storage.Search;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Imaging;
using Windows.UI.Xaml.Navigation;
using LaBoiteAChaussures.Common;
using Newtonsoft.Json;

// The Blank Page item template is documented at http://go.microsoft.com/fwlink/?LinkId=402352&clcid=0x409

namespace LaBoiteAChaussures
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainPage
    {
        private const string PhotosListDictionaryFileName = "photosListDictionary.txt";
        private const string YearListForBindingListFileName = "yearListForBindingList.txt";

        private readonly ObservableDictionary defaultViewModel = new ObservableDictionary();
        private readonly List<PhotoClass> photosList = new List<PhotoClass>();
        private readonly StorageFolder localFolder = ApplicationData.Current.LocalFolder;
        private readonly NavigationHelper navigationHelper;

        private Dictionary<string, IEnumerable<PhotoClass>> photosListDictionary = new Dictionary<string, IEnumerable<PhotoClass>>();
        private List<BindingYearData> yearListForBindingList = new List<BindingYearData>();
        private List<PhotoClass> randomPhotosList = new List<PhotoClass>();
        private List<ImageSource> imageListTemp = new List<ImageSource>();
        private ApplicationDataContainer localSettings = ApplicationData.Current.LocalSettings;

        public MainPage()
        {
            this.InitializeComponent();
            this.navigationHelper = new NavigationHelper(this);
            this.OobeWork();
            this.RetrievePictureData();
            this.SetString();

            // Hide Back navigation button
            SystemNavigationManager.GetForCurrentView().AppViewBackButtonVisibility = AppViewBackButtonVisibility.Collapsed;
        }

        public ObservableDictionary DefaultViewModel
        {
            get { return this.defaultViewModel; }
        }

        private void SetString()
        {
            this.PageTitle.Text = GetRessource("MainTitle");
            //this.RefreshAppBarButton.Label = GetRessource("LibraryRefreshAppButton");
            //this.InfosAppBarButton.Label = GetRessource("InfosAppButton");
        }

        public static string GetRessource(string ressourceName)
        {
            return Windows.ApplicationModel.Resources.ResourceLoader.GetForCurrentView().GetString(ressourceName);
        }

        private async void RetrievePictureData(bool forceReload = false)
        {
            LoadingProgressRing.IsActive = true;
            // RefreshAppBarButton.IsEnabled = false;
            this.photosListDictionary.Clear();
            this.photosList.Clear();
            this.yearListForBindingList.Clear();
            this.DefaultViewModel["Items"] = null;
            NameTextBlock.Text = "";
            OpenBox.Visibility = Visibility.Collapsed;

            StorageFile file1 = await this.TryGetFileFromLocalFolder(PhotosListDictionaryFileName);
            StorageFile file2 = await this.TryGetFileFromLocalFolder(YearListForBindingListFileName);

            if (file1 != null && file2 != null && !forceReload)
            {
                this.photosListDictionary = JsonConvert.DeserializeObject<Dictionary<string, IEnumerable<PhotoClass>>>((await FileIO.ReadLinesAsync(file1))[0]);
                var t = await FileIO.ReadLinesAsync(file2);
                this.yearListForBindingList = JsonConvert.DeserializeObject<List<BindingYearData>>(t[0]);
            }
            else
            {
                // no local file, we should retrieve the user data
                this.photosListDictionary = await this.GetPicturesFromUserLibrary();
            }

            await this.SetThumbnailImage();
            this.DefaultViewModel["Items"] = this.yearListForBindingList; // Add the items to the main binding items collection

            this.SetAllPicturesInTheBox();
            this.PrepareToOpenTheBox();
        }

        private async Task<Dictionary<string, IEnumerable<PhotoClass>>> GetPicturesFromUserLibrary()
        {
            var tempDico = new Dictionary<string, IEnumerable<PhotoClass>>();

            StorageFolderQueryResult query = KnownFolders.PicturesLibrary.CreateFolderQuery(CommonFolderQuery.GroupByYear);
            var f = (await query.GetFoldersAsync()).ToList();
            foreach (var storageFolder in f)
            {
                bool isError = false;
                try
                {
                    IReadOnlyList<StorageFile> fileList = (await storageFolder.GetFilesAsync(CommonFileQuery.DefaultQuery));
                    var picturesByYearList = (from p in fileList
                                              where p.FileType.ToLower().Contains(".jpg") || p.FileType.ToLower().Contains(".gif") || p.FileType.ToLower().Contains(".png")
                                              select p);

                    if (picturesByYearList.Any())
                    {
                        tempDico.Add(storageFolder.Name, PhotoClass.HydrateFromStorageFileList(picturesByYearList));
                        this.yearListForBindingList.Add(new BindingYearData
                        {
                            Title = storageFolder.Name,
                            Subtitle = picturesByYearList.ToList().Count + " " + GetRessource("MainPage_PictureWord")
                        });
                    }
                }
                catch (Exception e)
                {
                    isError = true;
                }
                if (isError) continue;
            }

            this.yearListForBindingList.Reverse(); // Reverse the order to have the old folder at the beginning (as on Pictures app)

            // Write data in local folder for storage
            await this.WriteFileInLocalFolder(PhotosListDictionaryFileName, tempDico);
            await this.WriteFileInLocalFolder(YearListForBindingListFileName, this.yearListForBindingList);

            return tempDico;
        }

        private async Task WriteFileInLocalFolder(string filename, object value)
        {
            var sampleFile = await this.localFolder.CreateFileAsync(filename, CreationCollisionOption.ReplaceExisting);
            await FileIO.WriteTextAsync(sampleFile, JsonConvert.SerializeObject(value));
        }

        private async Task SetThumbnailImage()
        {
            // int number = 1;

            foreach (var yearData in this.yearListForBindingList)
            {
                try
                {
                    IEnumerable<PhotoClass> t = this.photosListDictionary[yearData.Title];
                    var photoToDisplay = t.OrderBy(x => Guid.NewGuid()).ToList()[0];
                    var bitmapImage = new BitmapImage();
                    var storageFile = await StorageFile.GetFileFromPathAsync(photoToDisplay.Path);
                    bitmapImage.SetSource(await storageFile.OpenReadAsync());
                    yearData.ImageSource = bitmapImage;
                }

                // ReSharper disable once EmptyGeneralCatchClause
                catch (Exception)
                {
                    // we have no photo to display
                }

                //// Use the following line to create screenshot
                //string uri = "ms-appx:/Assets/" + number + ".jpg";
                //yearData.ImageSource = new BitmapImage(new Uri(uri));
                //number++;
            }
        }

        private void PrepareToOpenTheBox()
        {
            if (this.photosList.Count != 0)
            {
                this.randomPhotosList = this.photosList.OrderBy(x => Guid.NewGuid()).ToList();
                NameTextBlock.Text = this.randomPhotosList.Count + " " + GetRessource("MainPage_PictureWord");
                OpenBox.Visibility = Visibility.Visible;
                ItemGridView.Visibility = Visibility.Visible;
            }
            else
            {
                NameTextBlock.Text = GetRessource("MainPage_NoPictureInLibrary");
            }

            LoadingProgressRing.IsActive = false;
            // RefreshAppBarButton.IsEnabled = true;
        }

        private async Task<StorageFile> TryGetFileFromLocalFolder(string fileName)
        {
            StorageFile file = null;
            try
            {
                file = await this.localFolder.GetFileAsync(fileName);
            }
            // ReSharper disable once EmptyGeneralCatchClause
            catch
            {
            }

            return file;
        }

        private void OobeWork()
        {
            if (this.localSettings.Values["oobe"] == null)
            {
                LoadingProgressRing.Margin = new Thickness(0, 450, 0, 0);
            }
            else
            {
                LoadingProgressRing.Margin = new Thickness(0, 0, 0, 0);
                Oobe1Image.Visibility = Visibility.Collapsed;
            }

            this.localSettings.Values["oobe"] = true;
        }

        private void HamburgerButton_Click(object sender, RoutedEventArgs e)
        {
            this.MySplitView.IsPaneOpen = !MySplitView.IsPaneOpen;
        }

        private void MenuButton1_OnClick(object sender, RoutedEventArgs e)
        {
            this.PageTitle.Text = "Page 1";
        }

        private void MenuButton2_OnClick(object sender, RoutedEventArgs e)
        {
            this.PageTitle.Text = "Page 2";
        }

        private void Button_Click(object sender, TappedRoutedEventArgs e)
        {
            Frame.Navigate(typeof(PhotoPage), this.randomPhotosList);
        }

        private void ItemGridView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            this.photosList.Clear();
            foreach (var selectedItem in ItemGridView.SelectedItems)
            {
                this.photosList.AddRange(this.photosListDictionary[((BindingYearData)selectedItem).Title]);
            }

            if (ItemGridView.SelectedItems.Count == 0)
            {
                this.SetAllPicturesInTheBox();
            }

        }

        private void SetAllPicturesInTheBox()
        {
            foreach (var v in this.photosListDictionary)
            {
                this.photosList.AddRange(v.Value);
            }
        }

        private void OpenBox_PointerPressed(object sender, PointerRoutedEventArgs e)
        {
            OpenBox.Opacity = 0.5;
        }
    }
}