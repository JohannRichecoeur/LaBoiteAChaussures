using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using LaBoiteAChaussures.Common;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage;
using Windows.Storage.Streams;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media.Imaging;
using Windows.UI.Xaml.Navigation;

namespace LaBoiteAChaussures
{
    /// <summary>
    /// A page that displays details for a single item within a group while allowing gestures to
    /// flip through other items belonging to the same group.
    /// </summary>
    public sealed partial class PhotoPage
    {
        private readonly NavigationHelper navigationHelper;
        private int counter = 1;
        private List<PhotoClass> photosListToDisplay = new List<PhotoClass>();
        private PhotoClass currentPicture;
        private Visibility visibility = Visibility.Collapsed;
        private double initialPoint;
        private double endPoint;
        private string currentPhotoPath = "";

        public PhotoPage()
        {
            InitializeComponent();
            this.navigationHelper = new NavigationHelper(this);
            this.navigationHelper.LoadState += this.NavigationHelper_LoadState;
            MainGrid.PointerPressed += this.UIElement_OnPointerPressed;
            MainGrid.PointerReleased += this.UIElement_OnPointerExited;
            MainGrid.KeyDown += this.MainGridOnKeyUp;

            DataTransferManager dataTransferManager = DataTransferManager.GetForCurrentView();
            dataTransferManager.DataRequested += this.DataTransferManagerOnDataRequested;

            // Handle back navigation
            SystemNavigationManager.GetForCurrentView().AppViewBackButtonVisibility = AppViewBackButtonVisibility.Visible;
            SystemNavigationManager.GetForCurrentView().BackRequested +=
                (sender, args) =>
                {
                    Frame.Navigate(typeof(MainPage));
                };
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            this.navigationHelper.OnNavigatedTo(e);
        }

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            this.navigationHelper.OnNavigatedFrom(e);
        }

        private async void DataTransferManagerOnDataRequested(DataTransferManager sender, DataRequestedEventArgs e)
        {
            DataRequestDeferral deferral = e.Request.GetDeferral();
            e.Request.Data.Properties.Title = MainPage.GetRessource("PhotoShare");
            e.Request.Data.SetBitmap(RandomAccessStreamReference.CreateFromFile(await StorageFile.GetFileFromPathAsync(this.currentPicture.Path)));
            deferral.Complete();
        }

        private async void MainGridOnKeyUp(object sender, KeyRoutedEventArgs keyRoutedEventArgs)
        {
            if (keyRoutedEventArgs.Key == Windows.System.VirtualKey.Right)
            {
                await this.GoToNextPicture();
            }
            else if (keyRoutedEventArgs.Key == Windows.System.VirtualKey.Left)
            {
                await this.GoToPreviousPicture();
            }
            else if (keyRoutedEventArgs.Key == Windows.System.VirtualKey.Escape)
            {
                this.Frame.Navigate(typeof(MainPage));
            }
        }

        private async Task DisplayPicture()
        {
            try
            {
                this.currentPicture = this.photosListToDisplay[this.counter - 1];

                NameTextBlock.Text = this.currentPicture.Name;
                PathTextBlock.Text = this.currentPicture.Path;
                PhotoNumberTextBlock.Text = this.counter.ToString() + "/" + this.photosListToDisplay.Count;

                var pic = await StorageFile.GetFileFromPathAsync(this.currentPicture.Path);
                var properties = await pic.Properties.GetImagePropertiesAsync();
                if (properties != null)
                {
                    TakenDateTextBlock.Text = properties.DateTaken.ToString() ?? string.Empty;
                    MarqueTextBlock.Text = properties.CameraManufacturer;
                    ModeleTextBlock.Text = properties.CameraModel;
                }

                var stream = await pic.OpenReadAsync();
                var bitmapImage = new BitmapImage();
                bitmapImage.SetSource(stream);
                ImageBox.Source = bitmapImage;
            }
            catch (Exception)
            {
                this.Frame.Navigate(typeof(MainPage), "errorMessage");
            }
        }

        private async void NavigationHelper_LoadState(object sender, LoadStateEventArgs e)
        {
            this.photosListToDisplay = (List<PhotoClass>)e.NavigationParameter;
            await this.DisplayPicture();
        }

        private async void PreviousButton_Click(object sender, RoutedEventArgs e)
        {
            await this.GoToPreviousPicture();
        }

        private async void NextButton_Click(object sender, RoutedEventArgs e)
        {
            await this.GoToNextPicture();
        }

        private async Task GoToPreviousPicture()
        {
            if (this.counter == 1)
            {
                this.counter = this.photosListToDisplay.Count;
            }
            else
            {
                this.counter = this.counter - 1;
            }

            await this.DisplayPicture();
        }

        private async Task GoToNextPicture()
        {
            if (this.counter == this.photosListToDisplay.Count)
            {
                this.counter = 1;
            }
            else
            {
                this.counter++;
            }

            await this.DisplayPicture();
        }

        private void SetVisibility()
        {
            this.visibility = this.visibility == Visibility.Collapsed ? Visibility.Visible : Visibility.Collapsed;
            InfosGrid.Visibility = this.visibility;
        }

        private void UIElement_OnPointerPressed(object sender, PointerRoutedEventArgs e)
        {
            this.initialPoint = e.GetCurrentPoint(Window.Current.Content).RawPosition.X;
        }

        private async void UIElement_OnPointerExited(object sender, PointerRoutedEventArgs e)
        {
            this.endPoint = e.GetCurrentPoint(Window.Current.Content).RawPosition.X;
            if (this.initialPoint - this.endPoint > 50)
            {
                await this.GoToNextPicture();
            }
            else if (this.endPoint - this.initialPoint > 50)
            {
                await this.GoToPreviousPicture();
            }
            else
            {
                this.SetVisibility();
            }
        }
    }
}