# VelocityGrid.Native.Tests

Packaged Microsoft C++ unit-test project for native algorithms and state containers.

Tests cover viewport math, scroll clamping, LRU eviction, cached cell updates/formatting, request completion generations, and cancellation. Build **Debug | x64** (or the intended target configuration), deploy the packaged test application, and run tests through Visual Studio Test Explorer.

This project is not a standalone console runner. `dotnet test` does not execute these C++/WinUI packaged tests. When changing viewport, cache, scheduler, page, or formatting semantics, add a deterministic native test here and build both Debug and Release solution configurations.
