using System.Windows.Media.Imaging;

namespace BackgroundImageRemover.Models;

public class Channel : ObservableObject
{
    private string _name = "Channel";
    private bool _isVisible = true;
    private BitmapSource? _thumbnail;
    private ChannelType _channelType;

    public string Name
    {
        get => _name;
        set => SetProperty(ref _name, value);
    }

    public bool IsVisible
    {
        get => _isVisible;
        set => SetProperty(ref _isVisible, value);
    }

    public BitmapSource? Thumbnail
    {
        get => _thumbnail;
        set => SetProperty(ref _thumbnail, value);
    }

    public ChannelType ChannelType
    {
        get => _channelType;
        set => SetProperty(ref _channelType, value);
    }
}

public enum ChannelType
{
    Red,
    Green,
    Blue,
    Alpha,
    Gray,
    Indexed,
    SelectionMask
}