using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.Storage.Search;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media.Imaging;
using LaBoiteAChaussures.Common;
using Newtonsoft.Json;

namespace LaBoiteAChaussures
{
    public sealed partial class MainPage
    {
        public static ApplicationDataContainer LocalSettings = ApplicationData.Current.LocalSettings;

        private const string PhotosListDictionaryFileName = "photosListDictionary.txt";
        private const string YearListForBindingListFileName = "yearListForBindingList.txt";
        private const string PivotPhotos = "photos";
        private const string PivotVideos = "videos";
        private const string PivotSettings = "settings";


        private readonly List<PhotoClass> photosList = new List<PhotoClass>();
        private readonly StorageFolder localFolder = ApplicationData.Current.LocalFolder;

        private Dictionary<string, IEnumerable<PhotoClass>> photosListDictionary = new Dictionary<string, IEnumerable<PhotoClass>>();
        private List<BindingYearData> yearListForBindingList = new List<BindingYearData>();
        private List<PhotoClass> randomPhotosList = new List<PhotoClass>();
        private readonly List<string> picturesFormat = new List<string>() { ".jpg", ".gif", ".png" };


        public MainPage()
        {
            this.InitializeComponent();
            this.OobeWork();
            this.SelectPivot();
            this.SetString();

            // Hide Back navigation button
            SystemNavigationManager.GetForCurrentView().AppViewBackButtonVisibility = AppViewBackButtonVisibility.Collapsed;
        }

        private void SelectPivot()
        {
            switch ((string)Helper.GetLocalSettings(LocalSettingsValue.selectedpivot))
            {
                case PivotVideos:
                    this.DisplayVideosPivot();
                    break;
                case PivotSettings:
                    this.DisplaySettingsPivot();
                    break;
                default:
                    this.DisplayPhotosPivot();
                    break;
            }
        }

        public ObservableDictionary DefaultViewModel { get; } = new ObservableDictionary();

        private void SetString()
        {
            this.RefreshTextBlock.Text = Helper.GetRessource("LibraryRefreshAppButton");
            this.TextForEmptyGrid.Text = Helper.GetRessource("MainPage_NoPictureInLibrary");
            this.PhotosButtonName.Text = Helper.GetRessource("PicturesTitle");
            this.SettingsButtonName.Text = Helper.GetRessource("SettingsTitle");
            //this.InfosAppBarButton.Label = GetRessource("InfosAppButton");
        }

        private async void RetrievePictureData(bool forceReload = false)
        {
            this.TextForEmptyGrid.Visibility = Visibility.Collapsed;
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
            //this.yearListForBindingList.Clear();
            this.TextForEmptyGrid.Visibility = this.yearListForBindingList.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

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
                                              where picturesFormat.Any(p.FileType.ToLower().Contains)
                                              select p);

                    if (picturesByYearList.Any())
                    {
                        tempDico.Add(storageFolder.Name, PhotoClass.HydrateFromStorageFileList(picturesByYearList));
                        this.yearListForBindingList.Add(new BindingYearData
                        {
                            Title = storageFolder.Name,
                            Subtitle = picturesByYearList.ToList().Count + " " + Helper.GetRessource("MainPage_PictureWord")
                        });
                    }
                }
                catch (Exception)
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
                NameTextBlock.Text = this.randomPhotosList.Count + " " + Helper.GetRessource("MainPage_PictureWord");
                OpenBox.Visibility = Visibility.Visible;
                ItemGridView.Visibility = Visibility.Visible;
            }
            else
            {
                NameTextBlock.Text = Helper.GetRessource("MainPage_NoPictureInLibrary");
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
            if (!Helper.DoesLocalSettingsExists(LocalSettingsValue.oobe))
            {
                LoadingProgressRing.Margin = new Thickness(0, 450, 0, 0);
            }
            else
            {
                LoadingProgressRing.Margin = new Thickness(0, 0, 0, 0);
                Oobe1Image.Visibility = Visibility.Collapsed;

                Helper.SetLocalSettings(LocalSettingsValue.oobe, true);
            }

            if (!Helper.DoesLocalSettingsExists(LocalSettingsValue.selectedpivot))
            {
                Helper.SetLocalSettings(LocalSettingsValue.selectedpivot, PivotPhotos);
            }
        }

        private void HamburgerButton_Click(object sender, RoutedEventArgs e)
        {
            this.MySplitView.IsPaneOpen = !MySplitView.IsPaneOpen;
        }

        private void MenuPhotosClick(object sender, RoutedEventArgs e)
        {
            this.DisplayPhotosPivot();
        }

        private void MenuVideosClick(object sender, RoutedEventArgs e)
        {
            this.DisplayVideosPivot();
        }

        private void MenuSettingsClick(object sender, RoutedEventArgs e)
        {
            this.DisplaySettingsPivot();
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

            this.PrepareToOpenTheBox();
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

        private void RefreshTextBlock_Tapped(object sender, TappedRoutedEventArgs e)
        {
            this.RetrievePictureData(true);
        }

        private void Grid_PointerEntered(object sender, PointerRoutedEventArgs e)
        {
            if (this.ItemGridView.SelectedIndex == -1)
            {
                this.ItemGridView.IsMultiSelectCheckBoxEnabled = true;
            }
        }

        private void Grid_PointerExited(object sender, PointerRoutedEventArgs e)
        {
            if (this.ItemGridView.SelectedIndex == -1)
            {
                this.ItemGridView.IsMultiSelectCheckBoxEnabled = false;
            }
        }

        private void DisplayPhotosPivot()
        {
            this.RetrievePictureData();
            this.MySplitView.IsPaneOpen = false;
            this.PageTitle.Text = Helper.GetRessource("PicturesTitle");
            Helper.SetLocalSettings(LocalSettingsValue.selectedpivot, PivotPhotos);

            this.ItemGridView.Visibility = Visibility.Visible;
            this.SettingsGrid.Visibility = Visibility.Collapsed;
        }

        private void DisplayVideosPivot()
        {
            this.MySplitView.IsPaneOpen = false;
            this.PageTitle.Text = Helper.GetRessource("VideosTitle");
            Helper.SetLocalSettings(LocalSettingsValue.selectedpivot, PivotVideos);
        }

        private void DisplaySettingsPivot()
        {
            this.MySplitView.IsPaneOpen = false;
            this.PageTitle.Text = Helper.GetRessource("SettingsTitle");
            Helper.SetLocalSettings(LocalSettingsValue.selectedpivot, PivotSettings);

            this.ItemGridView.Visibility = Visibility.Collapsed;
            this.SettingsGrid.Visibility = Visibility.Visible;
        }
    }
}