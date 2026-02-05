# FluentAssertions Patterns Reference

## Contents
- Object Assertions
- Collection Assertions
- String Assertions
- Numeric Assertions
- Exception Assertions
- Async Assertions
- Equivalency Comparisons
- Anti-Patterns

## Object Assertions

### Null and Type Checking

```csharp
// GOOD - Clear intent
result.Should().NotBeNull();
result.Should().BeOfType<Tank>();
result.Should().BeAssignableTo<IEntity>();

// With custom message for debugging
tank.Should().NotBeNull("the tank service should always return initialized tanks");
```

### Property Assertions

```csharp
// GOOD - Assert specific properties
control.Name.Should().Be("Main Light");
control.Type.Should().Be(ControlType.Toggle);
control.State.Should().BeOfType<bool>().Which.Should().BeFalse();
```

## Collection Assertions

### Count and Content

```csharp
// Testing TankService.GetAllTanksAsync
var tanks = await service.GetAllTanksAsync();

tanks.Should().NotBeEmpty();
tanks.Should().HaveCount(4);
tanks.Should().HaveCountGreaterThan(0);
tanks.Should().ContainSingle(t => t.Type == TankType.LPG);
```

### Element Validation

```csharp
// GOOD - AllSatisfy for universal conditions
tanks.Should().AllSatisfy(tank =>
{
    tank.Id.Should().NotBe(Guid.Empty);
    tank.IsActive.Should().BeTrue();
    tank.CurrentLevel.Should().BeInRange(0, 100);
});

// GOOD - OnlyContain for type filtering
var alerts = await alertService.GetActiveAlertsAsync();
alerts.Should().OnlyContain(a => !a.Acknowledged);
```

### Ordering

```csharp
// Test alerts are ordered by severity
alerts.Should().BeInDescendingOrder(a => a.Severity);
alerts.Should().BeInAscendingOrder(a => a.Timestamp);
```

## String Assertions

```csharp
// GOOD - Specific string assertions
tank.Name.Should().NotBeNullOrWhiteSpace();
tank.Name.Should().StartWith("Fresh");
tank.SensorPlugin.Should().Contain("Simulated");

// Case-insensitive comparison
config.VanModel.Should().BeEquivalentTo("mercedes sprinter lwb");
```

## Numeric Assertions

```csharp
// GOOD - Range validation for tank levels
tank.CurrentLevel.Should().BeInRange(0, 100);
tank.Capacity.Should().BePositive();
tank.AlertLevel.Should().BeGreaterThanOrEqualTo(0);
tank.AlertLevel.Should().BeLessThanOrEqualTo(100);

// Approximate comparisons for floating point
sensorReading.Should().BeApproximately(75.5, precision: 0.1);
```

## Exception Assertions

### Synchronous Methods

```csharp
// GOOD - Testing validation exceptions
Action act = () => new Tank { CurrentLevel = -1 };
act.Should().Throw<ArgumentOutOfRangeException>()
   .WithMessage("*cannot be negative*");
```

### Async Methods

```csharp
// GOOD - Async exception testing for services
await service.Invoking(s => s.GetTankByIdAsync(Guid.Empty))
    .Should().ThrowAsync<ArgumentException>()
    .WithMessage("*Tank ID cannot be empty*");

// Testing plugin initialization failures
await plugin.Invoking(p => p.InitializeAsync(invalidConfig))
    .Should().ThrowAsync<InvalidOperationException>();
```

## Async Assertions

```csharp
// GOOD - Awaiting async results then asserting
var result = await controlService.SetStateAsync(controlId, true);
result.Should().BeTrue();

// GOOD - Testing async completion
var task = backgroundService.StartAsync(CancellationToken.None);
await task.Should().CompleteWithinAsync(TimeSpan.FromSeconds(5));
```

## Equivalency Comparisons

### Deep Object Comparison

```csharp
// GOOD - Compare entire objects by value
var expectedTank = new Tank { Id = id, Name = "Test", Type = TankType.FreshWater };
actualTank.Should().BeEquivalentTo(expectedTank);

// Exclude auto-generated properties
actualTank.Should().BeEquivalentTo(expectedTank, options => options
    .Excluding(t => t.LastUpdated)
    .Excluding(t => t.Id));
```

### Collection Equivalence

```csharp
// GOOD - Order-independent collection comparison
actualTanks.Should().BeEquivalentTo(expectedTanks);

// With strict ordering
actualAlerts.Should().BeEquivalentTo(expectedAlerts, options => options
    .WithStrictOrdering());
```

## Anti-Patterns

### WARNING: Multiple Assertions Without Context

**The Problem:**

```csharp
// BAD - No context when assertion fails
Assert.NotNull(result);
Assert.Equal(3, result.Count);
```

**Why This Breaks:** When a test fails, you don't know which assertion or why.

**The Fix:**

```csharp
// GOOD - Clear failure messages
result.Should().NotBeNull("service should return initialized collection");
result.Should().HaveCount(3, "default configuration has 3 tanks");
```

### WARNING: Using BeEquivalentTo for Exact Matches

**The Problem:**

```csharp
// BAD - BeEquivalentTo ignores order and uses fuzzy matching
result.Id.Should().BeEquivalentTo(expectedId);
```

**Why This Breaks:** `BeEquivalentTo` is for complex objects. For simple values, it adds overhead and can hide issues.

**The Fix:**

```csharp
// GOOD - Use Be() for exact value matches
result.Id.Should().Be(expectedId);
```

### WARNING: Asserting on Null Without NotBeNull First

**The Problem:**

```csharp
// BAD - NullReferenceException if result is null
result.Name.Should().Be("Expected");
```

**The Fix:**

```csharp
// GOOD - Guard against null first
result.Should().NotBeNull();
result.Name.Should().Be("Expected");