using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Xunit;

namespace TacticalGame.Tests
{
    public class SerializationSpikeTests
    {
        private class NoConstructor
        {
            public string Name { get; set; } = "";
            public int Value { get; set; }
            public List<int> Items { get; set; } = new();
        }

        private class ReadonlyWithConstructor
        {
            private readonly string _name;
            private readonly int _value;
            private readonly List<int> _items;

            public string Name => _name;
            public int Value => _value;
            public IReadOnlyList<int> Items => _items;

            public ReadonlyWithConstructor(string name, int value, List<int> items)
            {
                _name = name;
                _value = value;
                _items = items;
            }
        }

        private class ReadonlyNoConstructor
        {
            private readonly string _name;
            private readonly int _value;

            public string Name => _name;
            public int Value => _value;
        }

        [Fact]
        public void NoConstructor_RoundTrips()
        {
            var original = new NoConstructor { Name = "Test", Value = 42, Items = new List<int> { 1, 2, 3 } };
            var json = JsonConvert.SerializeObject(original);
            var loaded = JsonConvert.DeserializeObject<NoConstructor>(json)!;

            Assert.Equal("Test", loaded.Name);
            Assert.Equal(42, loaded.Value);
            Assert.Equal(3, loaded.Items.Count);
        }

        [Fact]
        public void ReadonlyWithConstructor_RoundTrips()
        {
            var original = new ReadonlyWithConstructor("Test", 42, new List<int> { 1, 2, 3 });
            var json = JsonConvert.SerializeObject(original);
            var loaded = JsonConvert.DeserializeObject<ReadonlyWithConstructor>(json)!;

            Assert.Equal("Test", loaded.Name);
            Assert.Equal(42, loaded.Value);
            Assert.Equal(3, loaded.Items.Count);
        }

        [Fact]
        public void ReadonlyNoConstructor_LosesValues()
        {
            var json = "{\"Name\":\"Test\",\"Value\":42}";
            var loaded = JsonConvert.DeserializeObject<ReadonlyNoConstructor>(json)!;

            Assert.Null(loaded.Name);  // LOST — no constructor, readonly field
            Assert.Equal(0, loaded.Value);
        }

        // Spike: private set without constructor param does NOT round-trip
        private class PrivateSetNoParam
        {
            public string Name { get; }
            public int Amount { get; }
            public int AppliedValue { get; private set; }

            public PrivateSetNoParam(string name, int amount)
            {
                Name = name;
                Amount = amount;
            }

            public void Apply() => AppliedValue = Amount * 2;
        }

        [Fact]
        public void PrivateSet_WithoutConstructorParam_LosesValue()
        {
            var original = new PrivateSetNoParam("Test", 10);
            original.Apply();

            var json = JsonConvert.SerializeObject(original);
            var loaded = JsonConvert.DeserializeObject<PrivateSetNoParam>(json)!;

            Assert.Equal(0, loaded.AppliedValue); // LOST — not in constructor
        }

        // Spike: adding optional constructor param fixes it
        private class PrivateSetWithParam
        {
            public string Name { get; }
            public int Amount { get; }
            public int AppliedValue { get; private set; }

            public PrivateSetWithParam(string name, int amount, int appliedValue = 0)
            {
                Name = name;
                Amount = amount;
                AppliedValue = appliedValue;
            }

            public void Apply() => AppliedValue = Amount * 2;
        }

        [Fact]
        public void PrivateSet_WithConstructorParam_RoundTrips()
        {
            var original = new PrivateSetWithParam("Test", 10);
            original.Apply();

            var json = JsonConvert.SerializeObject(original);
            var loaded = JsonConvert.DeserializeObject<PrivateSetWithParam>(json)!;

            Assert.Equal(20, loaded.AppliedValue); // WORKS — matched via constructor
        }

        // Spike: does MemberSerialization.Fields serialize all fields including readonly?
        [JsonObject(MemberSerialization.Fields)]
        private class FieldsMode
        {
            private readonly string _name;
            private readonly int _value;
            private readonly List<int> _items;
            public int AppliedValue { get; private set; }

            public string Name => _name;
            public int Value => _value;
            public IReadOnlyList<int> Items => _items;

            public FieldsMode(string name, int value, List<int> items)
            {
                _name = name;
                _value = value;
                _items = items;
            }

            public void Apply() => AppliedValue = Value * 2;
        }

        [Fact]
        public void FieldsMode_RoundTrips_ReadonlyFields()
        {
            var original = new FieldsMode("Test", 42, new List<int> { 1, 2, 3 });
            original.Apply();

            var json = JsonConvert.SerializeObject(original);
            var loaded = JsonConvert.DeserializeObject<FieldsMode>(json)!;

            Assert.Equal("Test", loaded.Name);
            Assert.Equal(42, loaded.Value);
            Assert.Equal(3, loaded.Items.Count);
            Assert.Equal(84, loaded.AppliedValue);
        }
        // Spike: can a struct be used as a dictionary key?
        private readonly struct MyKey : IEquatable<MyKey>
        {
            public int X { get; }
            public int Y { get; }

            public MyKey(int x, int y) { X = x; Y = y; }

            public bool Equals(MyKey other) => X == other.X && Y == other.Y;
            public override bool Equals(object? obj) => obj is MyKey other && Equals(other);
            public override int GetHashCode() => HashCode.Combine(X, Y);
            public override string ToString() => $"({X}, {Y})";
        }

        [Fact]
        public void StructDictKey_RoundTrips()
        {
            var dict = new Dictionary<MyKey, string>
            {
                { new MyKey(1, 2), "hello" },
                { new MyKey(3, 4), "world" }
            };

            var json = JsonConvert.SerializeObject(dict, Formatting.Indented);
            var loaded = JsonConvert.DeserializeObject<Dictionary<MyKey, string>>(json)!;

            Assert.Equal(2, loaded.Count);
            Assert.Equal("hello", loaded[new MyKey(1, 2)]);
            Assert.Equal("world", loaded[new MyKey(3, 4)]);
        }
    }
}
