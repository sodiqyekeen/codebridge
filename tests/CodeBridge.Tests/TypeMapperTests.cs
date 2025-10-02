using CodeBridge.Core.Models;
using CodeBridge.Core.Services;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace CodeBridge.Tests;

/// <summary>
/// Unit tests for TypeMapper service.
/// </summary>
public sealed class TypeMapperTests
{
    private readonly Mock<ILogger<TypeMapper>> _loggerMock;
    private readonly TypeMapper _typeMapper;

    public TypeMapperTests()
    {
        _loggerMock = new Mock<ILogger<TypeMapper>>();
        _typeMapper = new TypeMapper(_loggerMock.Object);
    }

    #region Primitive Type Mapping Tests

    [Theory]
    [InlineData("string", "string")]
    [InlineData("int", "number")]
    [InlineData("long", "number")]
    [InlineData("decimal", "number")]
    [InlineData("double", "number")]
    [InlineData("float", "number")]
    [InlineData("bool", "boolean")]
    [InlineData("DateTime", "string")] // DateTime maps to string for JSON serialization
    [InlineData("Guid", "string")]
    public void MapToTypeScript_PrimitiveTypes_ReturnsCorrectMapping(string csharpType, string expectedTsType)
    {
        // Act
        var result = _typeMapper.MapToTypeScript(csharpType);

        // Assert
        Assert.Equal(expectedTsType, result);
    }

    [Theory]
    [InlineData("System.String", "string")]
    [InlineData("System.Int32", "number")]
    [InlineData("System.Boolean", "boolean")]
    [InlineData("System.DateTime", "string")] // DateTime maps to string for JSON serialization
    [InlineData("System.Guid", "string")]
    public void MapToTypeScript_FullyQualifiedPrimitiveTypes_ReturnsCorrectMapping(string csharpType, string expectedTsType)
    {
        // Act
        var result = _typeMapper.MapToTypeScript(csharpType);

        // Assert
        Assert.Equal(expectedTsType, result);
    }

    #endregion

    #region Nullable Type Mapping Tests

    [Theory]
    [InlineData("string?", "string | null")]
    [InlineData("int?", "number | null")]
    [InlineData("bool?", "boolean | null")]
    [InlineData("DateTime?", "string | null")] // DateTime maps to string
    public void MapToTypeScript_NullableTypes_ReturnsCorrectMapping(string csharpType, string expectedTsType)
    {
        // Act
        var result = _typeMapper.MapToTypeScript(csharpType);

        // Assert
        Assert.Equal(expectedTsType, result);
    }

    #endregion

    #region Array Type Mapping Tests

    [Theory]
    [InlineData("string[]", "string[]")]
    [InlineData("int[]", "number[]")]
    [InlineData("bool[]", "boolean[]")]
    public void MapToTypeScript_ArrayTypes_ReturnsCorrectMapping(string csharpType, string expectedTsType)
    {
        // Act
        var result = _typeMapper.MapToTypeScript(csharpType);

        // Assert
        Assert.Equal(expectedTsType, result);
    }

    #endregion

    #region Collection Type Mapping Tests

    [Theory]
    [InlineData("List<string>", "string[]")]
    [InlineData("IList<int>", "number[]")]
    [InlineData("IEnumerable<bool>", "boolean[]")]
    public void MapToTypeScript_CollectionTypes_ReturnsCorrectMapping(string csharpType, string expectedTsType)
    {
        // Act
        var result = _typeMapper.MapToTypeScript(csharpType);

        // Assert
        Assert.Equal(expectedTsType, result);
    }

    [Theory]
    [InlineData("Dictionary<string, int>", "Record<string, number>")]
    [InlineData("IDictionary<string, bool>", "Record<string, boolean>")]
    public void MapToTypeScript_DictionaryTypes_ReturnsCorrectMapping(string csharpType, string expectedTsType)
    {
        // Act
        var result = _typeMapper.MapToTypeScript(csharpType);

        // Assert
        Assert.Equal(expectedTsType, result);
    }

    #endregion

    #region Generic Type Mapping Tests

    [Theory]
    [InlineData("Task<string>", "string")] // Task unwraps to inner type
    [InlineData("Task<int>", "number")]
    public void MapToTypeScript_TaskTypes_UnwrapsToInnerType(string csharpType, string expectedTsType)
    {
        // Act
        var result = _typeMapper.MapToTypeScript(csharpType);

        // Assert
        Assert.Equal(expectedTsType, result);
    }

    [Theory]
    [InlineData("Result<string>", "Result<string>")] // Result keeps its wrapper
    [InlineData("Result<int>", "Result<number>")]
    public void MapToTypeScript_ResultTypes_KeepsWrapper(string csharpType, string expectedTsType)
    {
        // Act
        var result = _typeMapper.MapToTypeScript(csharpType);

        // Assert
        Assert.Equal(expectedTsType, result);
    }

    #endregion

    #region TypeInfo Mapping Tests

    [Fact]
    public void MapToTypeScript_TypeInfo_ReturnsTypeName()
    {
        // Arrange
        var typeInfo = new TypeInfo
        {
            Name = "CustomerDto",
            FullName = "MyApi.Dtos.CustomerDto",
            IsNullable = false,
            IsEnum = false
        };

        // Act
        var result = _typeMapper.MapToTypeScript(typeInfo);

        // Assert
        Assert.Equal("CustomerDto", result);
    }

    [Fact]
    public void MapToTypeScript_NullableTypeInfo_ReturnsNullableTypeName()
    {
        // Arrange
        var typeInfo = new TypeInfo
        {
            Name = "CustomerDto",
            FullName = "MyApi.Dtos.CustomerDto",
            IsNullable = true,
            IsEnum = false
        };

        // Act
        var result = _typeMapper.MapToTypeScript(typeInfo);

        // Assert
        Assert.Equal("CustomerDto | null", result);
    }

    #endregion

    #region Custom Mapping Tests

    [Fact]
    public void RegisterCustomMapping_CustomType_UsesCustomMapping()
    {
        // Arrange
        _typeMapper.RegisterCustomMapping("MyCustomType", "CustomTypeScript");

        // Act
        var result = _typeMapper.MapToTypeScript("MyCustomType");

        // Assert
        Assert.Equal("CustomTypeScript", result);
    }

    #endregion

    #region IsPrimitiveType Tests

    [Theory]
    [InlineData("string", true)]
    [InlineData("int", true)]
    [InlineData("bool", true)]
    [InlineData("DateTime", true)]
    [InlineData("Guid", true)]
    [InlineData("CustomerDto", false)]
    public void IsPrimitiveType_ChecksCorrectly(string typeName, bool expected)
    {
        // Act
        var result = _typeMapper.IsPrimitiveType(typeName);

        // Assert
        Assert.Equal(expected, result);
    }

    #endregion

    #region Edge Cases

    [Fact]
    public void MapToTypeScript_NullTypeInfo_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => _typeMapper.MapToTypeScript((TypeInfo)null!));
    }

    [Theory]
    [InlineData(null, "any")]
    [InlineData("", "any")]
    public void MapToTypeScript_NullOrEmptyString_ReturnsAny(string? typeName, string expected)
    {
        // Act
        var result = _typeMapper.MapToTypeScript(typeName!);

        // Assert
        Assert.Equal(expected, result);
    }

    #endregion
}
