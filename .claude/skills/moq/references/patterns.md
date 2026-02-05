# Moq Patterns Reference

## Contents
- Setup Patterns
- Argument Matching
- Verification Patterns
- Callback and Capture Patterns
- Anti-Patterns

## Setup Patterns

### Sequential Returns

When a method is called multiple times:

```csharp
var mockSensor = new Mock<ISensorPlugin>();
mockSensor
    .SetupSequence(x => x.ReadValueAsync("tank-1", It.IsAny<CancellationToken>()))
    .ReturnsAsync(50.0)  // First call
    .ReturnsAsync(75.0)  // Second call
    .ReturnsAsync(100.0); // Third call
```

### Conditional Returns

```csharp
var mockService = new Mock<IControlService>();
mockService
    .Setup(x => x.GetControlByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
    .ReturnsAsync((Guid id, CancellationToken _) => 
        id == knownId ? existingControl : null);
```

### Throwing Exceptions

```csharp
var mockPlugin = new Mock<IControlPlugin>();
mockPlugin
    .Setup(x => x.SetStateAsync("invalid-id", It.IsAny<object>(), It.IsAny<CancellationToken>()))
    .ThrowsAsync(new InvalidOperationException("Device not found"));
```

## Argument Matching

### It.IsAny<T>() - Match Any Value

```csharp
// GOOD - CancellationToken usually doesn't matter in tests
mockService.Setup(x => x.GetAllTanksAsync(It.IsAny<CancellationToken>()))
    .ReturnsAsync(tanks);
```

### It.Is<T>() - Match with Predicate

```csharp
// GOOD - Verify specific argument conditions
mockService.Setup(x => x.UpdateTankAsync(
    It.Is<Tank>(t => t.CurrentLevel >= 0 && t.CurrentLevel <= 100),
    It.IsAny<CancellationToken>()))
    .ReturnsAsync(true);
```

### WARNING: Over-Specific Matching

**The Problem:**

```csharp
// BAD - Brittle test, breaks if any Tank property changes
mockService.Setup(x => x.UpdateTankAsync(
    It.Is<Tank>(t => 
        t.Id == expectedId && 
        t.Name == "Fresh Water" && 
        t.Type == TankType.FreshWater &&
        t.Capacity == 100.0),
    It.IsAny<CancellationToken>()))
    .ReturnsAsync(true);
```

**Why This Breaks:**
1. Test fails if any unrelated property changes
2. Doesn't test actual business logic
3. Creates maintenance burden

**The Fix:**

```csharp
// GOOD - Only match what matters for this test
mockService.Setup(x => x.UpdateTankAsync(
    It.Is<Tank>(t => t.Id == expectedId),
    It.IsAny<CancellationToken>()))
    .ReturnsAsync(true);
```

## Verification Patterns

### Basic Verification

```csharp
// Verify method was called exactly once
mockService.Verify(x => x.GetAllTanksAsync(It.IsAny<CancellationToken>()), Times.Once);

// Verify method was never called
mockService.Verify(x => x.DeleteTankAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
```

### Verification with Times

```csharp
mockHubContext.Verify(
    x => x.Clients.Group("tanks").SendAsync(
        "TankLevelUpdated",
        It.IsAny<object[]>(),
        It.IsAny<CancellationToken>()),
    Times.AtLeastOnce);
```

### WARNING: Verify Without Setup

**The Problem:**

```csharp
// BAD - Verifying a call that was never set up
var mockService = new Mock<ITankService>();
// ... no Setup() ...
await controller.RefreshTanks();
mockService.Verify(x => x.RefreshAllLevelsAsync(It.IsAny<CancellationToken>()), Times.Once);
// This passes even if method throws because strict mode isn't enabled
```

**The Fix:**

```csharp
// GOOD - Use MockBehavior.Strict or Setup before Verify
var mockService = new Mock<ITankService>(MockBehavior.Strict);
mockService.Setup(x => x.RefreshAllLevelsAsync(It.IsAny<CancellationToken>()))
    .Returns(Task.CompletedTask);

await controller.RefreshTanks();
mockService.Verify(x => x.RefreshAllLevelsAsync(It.IsAny<CancellationToken>()), Times.Once);
```

## Callback and Capture Patterns

### Capturing Arguments

```csharp
Tank capturedTank = null;
mockService
    .Setup(x => x.UpdateTankAsync(It.IsAny<Tank>(), It.IsAny<CancellationToken>()))
    .Callback<Tank, CancellationToken>((tank, _) => capturedTank = tank)
    .ReturnsAsync(true);

await sut.UpdateTankLevel(tankId, 75.0);

capturedTank.Should().NotBeNull();
capturedTank.CurrentLevel.Should().Be(75.0);
```

### Side Effects in Callbacks

```csharp
var savedAlerts = new List<Alert>();
mockAlertService
    .Setup(x => x.CreateAlertAsync(It.IsAny<Alert>(), It.IsAny<CancellationToken>()))
    .Callback<Alert, CancellationToken>((alert, _) => savedAlerts.Add(alert))
    .ReturnsAsync((Alert a, CancellationToken _) => a);
```

## Anti-Patterns

### WARNING: Mocking Concrete Classes

**The Problem:**

```csharp
// BAD - Mocking concrete TankService instead of ITankService
var mockService = new Mock<TankService>(mockLogger.Object, mockStore.Object);
```

**Why This Breaks:**
1. Requires virtual methods
2. Runs real constructor code
3. Defeats purpose of isolation

**The Fix:**

```csharp
// GOOD - Mock the interface
var mockService = new Mock<ITankService>();
```

### WARNING: Testing Mock Behavior

**The Problem:**

```csharp
// BAD - This tests Moq, not your code
mockService.Setup(x => x.GetTankAsync(testId, It.IsAny<CancellationToken>()))
    .ReturnsAsync(testTank);

var result = await mockService.Object.GetTankAsync(testId, CancellationToken.None);
result.Should().Be(testTank); // Useless - just proves Moq works
```

**The Fix:**

```csharp
// GOOD - Test your actual code
var controller = new TanksController(mockService.Object);
var result = await controller.GetTank(testId);
result.Value.Should().BeEquivalentTo(testTank);