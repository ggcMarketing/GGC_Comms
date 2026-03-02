using IndustrialGateway.Core.Models;

namespace IndustrialGateway.UI.ViewModels;

/// <summary>
/// ViewModel wrapper for TagDefinition with test value support
/// </summary>
public class TagViewModel : ViewModelBase
{
    private readonly TagDefinition _tag;
    private string _testValue = "0";

    public TagDefinition Tag => _tag;

    public string Id => _tag.Id;
    
    public string Name
    {
        get => _tag.Name;
        set
        {
            if (_tag.Name != value)
            {
                _tag.Name = value;
                OnPropertyChanged();
            }
        }
    }

    public string Address
    {
        get => _tag.Address;
        set
        {
            if (_tag.Address != value)
            {
                _tag.Address = value;
                OnPropertyChanged();
            }
        }
    }

    public TagDataType DataType
    {
        get => _tag.DataType;
        set
        {
            if (_tag.DataType != value)
            {
                _tag.DataType = value;
                OnPropertyChanged();
                // Reset test value when data type changes
                TestValue = GetDefaultTestValue(value).ToString() ?? "0";
            }
        }
    }

    public bool IsReadOnly
    {
        get => _tag.IsReadOnly;
        set
        {
            if (_tag.IsReadOnly != value)
            {
                _tag.IsReadOnly = value;
                OnPropertyChanged();
            }
        }
    }

    public int ScanRateMs
    {
        get => _tag.ScanRateMs;
        set
        {
            if (_tag.ScanRateMs != value)
            {
                _tag.ScanRateMs = value;
                OnPropertyChanged();
            }
        }
    }

    public string? Description
    {
        get => _tag.Description;
        set
        {
            if (_tag.Description != value)
            {
                _tag.Description = value;
                OnPropertyChanged();
            }
        }
    }

    /// <summary>
    /// Test/simulation value for Producer mode
    /// </summary>
    public string TestValue
    {
        get => _testValue;
        set => SetProperty(ref _testValue, value);
    }

    public TagViewModel(TagDefinition tag)
    {
        _tag = tag ?? throw new ArgumentNullException(nameof(tag));
        _testValue = GetDefaultTestValue(tag.DataType).ToString() ?? "0";
    }

    public object GetTestValueAsObject()
    {
        try
        {
            return DataType switch
            {
                TagDataType.Bool => bool.Parse(TestValue),
                TagDataType.Int16 => short.Parse(TestValue),
                TagDataType.UInt16 => ushort.Parse(TestValue),
                TagDataType.Int32 => int.Parse(TestValue),
                TagDataType.UInt32 => uint.Parse(TestValue),
                TagDataType.Float => float.Parse(TestValue),
                TagDataType.Double => double.Parse(TestValue),
                _ => 0
            };
        }
        catch
        {
            return GetDefaultTestValue(DataType);
        }
    }

    private static object GetDefaultTestValue(TagDataType dataType)
    {
        return dataType switch
        {
            TagDataType.Bool => true,
            TagDataType.Int16 => (short)100,
            TagDataType.UInt16 => (ushort)100,
            TagDataType.Int32 => 1000,
            TagDataType.UInt32 => 1000U,
            TagDataType.Float => 123.45f,
            TagDataType.Double => 123.45,
            _ => 0
        };
    }
}
