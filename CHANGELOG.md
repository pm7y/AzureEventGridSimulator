# Changelog

## [5.1.0](https://github.com/pm7y/AzureEventGridSimulator/compare/5.0.0...5.1.0) (2026-06-05)


### Features

* **cloudevents:** preserve extension attributes through ingestion and delivery ([#286](https://github.com/pm7y/AzureEventGridSimulator/issues/286)) ([f98124e](https://github.com/pm7y/AzureEventGridSimulator/commit/f98124e35ee19491b996158193aee10889a38102))


### Dependencies

* **actions:** bump googleapis/release-please-action ([#279](https://github.com/pm7y/AzureEventGridSimulator/issues/279)) ([9215283](https://github.com/pm7y/AzureEventGridSimulator/commit/92152836584ed09c72ec0e332362afd351bf5de7))
* **actions:** bump the actions group across 1 directory with 6 updates ([#267](https://github.com/pm7y/AzureEventGridSimulator/issues/267)) ([bf83089](https://github.com/pm7y/AzureEventGridSimulator/commit/bf8308967c9f4464f219927f7ef0fcaa35d47ac2))
* **actions:** bump the actions group with 2 updates ([#275](https://github.com/pm7y/AzureEventGridSimulator/issues/275)) ([88b986a](https://github.com/pm7y/AzureEventGridSimulator/commit/88b986a9860d45436d2cc104d74fb1a9e050f15e))
* **nuget:** Bump coverlet.collector from 8.0.1 to 10.0.0 ([#278](https://github.com/pm7y/AzureEventGridSimulator/issues/278)) ([2017c51](https://github.com/pm7y/AzureEventGridSimulator/commit/2017c5101dc6498daf402e50a3df4749a37de1a4))
* **nuget:** Bump the minor-and-patch group with 12 updates ([#280](https://github.com/pm7y/AzureEventGridSimulator/issues/280)) ([56e522b](https://github.com/pm7y/AzureEventGridSimulator/commit/56e522bdacd8db8aefa82b2e68918a28228d0b21))
* **nuget:** Bump the minor-and-patch group with 13 updates ([#284](https://github.com/pm7y/AzureEventGridSimulator/issues/284)) ([a68a10e](https://github.com/pm7y/AzureEventGridSimulator/commit/a68a10e75643ba2ff04d3225772e9219c5d8cf6d))
* **nuget:** Bump the minor-and-patch group with 4 updates ([#277](https://github.com/pm7y/AzureEventGridSimulator/issues/277)) ([063b12d](https://github.com/pm7y/AzureEventGridSimulator/commit/063b12dc0d6ffce0f35adc0a9966b92a506ee6de))
* **nuget:** Bump the minor-and-patch group with 5 updates ([#270](https://github.com/pm7y/AzureEventGridSimulator/issues/270)) ([38bd900](https://github.com/pm7y/AzureEventGridSimulator/commit/38bd900f7afd33e901e0a2807413f9ead5d792e4))
* **nuget:** Bump the minor-and-patch group with 6 updates ([#274](https://github.com/pm7y/AzureEventGridSimulator/issues/274)) ([7beb463](https://github.com/pm7y/AzureEventGridSimulator/commit/7beb463fa5db2cebbc2934ad0eecd6781057a61b))
* **nuget:** Bump the minor-and-patch group with 7 updates ([#268](https://github.com/pm7y/AzureEventGridSimulator/issues/268)) ([68bcaa3](https://github.com/pm7y/AzureEventGridSimulator/commit/68bcaa3ebc1b9f4aac064fa739ea73a35f7e7bcc))
* **nuget:** Bump the minor-and-patch group with 7 updates ([#271](https://github.com/pm7y/AzureEventGridSimulator/issues/271)) ([d75b124](https://github.com/pm7y/AzureEventGridSimulator/commit/d75b1240b0e6d6060a30986c949073998d1c95f0))
* **nuget:** Bump the minor-and-patch group with 8 updates ([#272](https://github.com/pm7y/AzureEventGridSimulator/issues/272)) ([3b08f88](https://github.com/pm7y/AzureEventGridSimulator/commit/3b08f881cab79577e55838f15ad7e0cf8b187ba3))
* **nuget:** Bump the minor-and-patch group with 9 updates ([#276](https://github.com/pm7y/AzureEventGridSimulator/issues/276)) ([b78addb](https://github.com/pm7y/AzureEventGridSimulator/commit/b78addb29f7e2482bb6b248609d4389d3dfe89b1))

## [5.0.0](https://github.com/pm7y/AzureEventGridSimulator/compare/4.6.3...5.0.0) (2026-02-18)


### ⚠ BREAKING CHANGES

* Service Bus message bodies are now single JSON objects instead of single-element arrays. Consumers that parse the array format will need to be updated.

### Bug Fixes

* integrate DOMPurify to sanitize innerHTML assignments in dashboard ([#252](https://github.com/pm7y/AzureEventGridSimulator/issues/252)) ([46bfac5](https://github.com/pm7y/AzureEventGridSimulator/commit/46bfac57a03057b749232bd0ba374de5e338e265))
* Send Service Bus events as single objects instead of arrays ([#261](https://github.com/pm7y/AzureEventGridSimulator/issues/261)) ([d033b19](https://github.com/pm7y/AzureEventGridSimulator/commit/d033b198b229418ca0a6b565221d242545d32576))


### Dependencies

* **nuget:** Bump the minor-and-patch group with 2 updates ([#253](https://github.com/pm7y/AzureEventGridSimulator/issues/253)) ([786b8e5](https://github.com/pm7y/AzureEventGridSimulator/commit/786b8e56f4673b93c8ed9ed0f868c566e7376347))
* **nuget:** Bump the minor-and-patch group with 2 updates ([#254](https://github.com/pm7y/AzureEventGridSimulator/issues/254)) ([c90650a](https://github.com/pm7y/AzureEventGridSimulator/commit/c90650a21b3467e2390f420d0148564a2d7aadd7))
* **nuget:** Bump the minor-and-patch group with 2 updates ([#260](https://github.com/pm7y/AzureEventGridSimulator/issues/260)) ([9497ecf](https://github.com/pm7y/AzureEventGridSimulator/commit/9497ecf56e3c16533287389496bdb9177854db38))
* **nuget:** Bump the minor-and-patch group with 3 updates ([#249](https://github.com/pm7y/AzureEventGridSimulator/issues/249)) ([a43306c](https://github.com/pm7y/AzureEventGridSimulator/commit/a43306cbf949be95f1009389c36ed2acbc4c8e70))
* **nuget:** Bump the minor-and-patch group with 4 updates ([#256](https://github.com/pm7y/AzureEventGridSimulator/issues/256)) ([ebfb116](https://github.com/pm7y/AzureEventGridSimulator/commit/ebfb116d8e790f43b484a649ca9bb62f9a145e47))
* **nuget:** Bump the minor-and-patch group with 6 updates ([#257](https://github.com/pm7y/AzureEventGridSimulator/issues/257)) ([1fc6069](https://github.com/pm7y/AzureEventGridSimulator/commit/1fc6069e201cb77ef7f3a98b7c2e6ebe0b4e9d70))
* **nuget:** Bump the minor-and-patch group with 9 updates ([#262](https://github.com/pm7y/AzureEventGridSimulator/issues/262)) ([bae6b1b](https://github.com/pm7y/AzureEventGridSimulator/commit/bae6b1b8781e6e0d0e90436fa46b386e7e2f1ebf))

## [4.6.3](https://github.com/pm7y/AzureEventGridSimulator/compare/4.6.2...4.6.3) (2025-12-26)


### Bug Fixes

* release ([#247](https://github.com/pm7y/AzureEventGridSimulator/issues/247)) ([84b41dd](https://github.com/pm7y/AzureEventGridSimulator/commit/84b41dd61e2d5a5ee4ebb15c3aa0e27c5f884f39))

## [4.6.2](https://github.com/pm7y/AzureEventGridSimulator/compare/4.6.1...4.6.2) (2025-12-26)


### Bug Fixes

* fix-release ([#245](https://github.com/pm7y/AzureEventGridSimulator/issues/245)) ([c15dc81](https://github.com/pm7y/AzureEventGridSimulator/commit/c15dc8163bd0684e2a503066de7e3dc327fa0481))

## [4.6.1](https://github.com/pm7y/AzureEventGridSimulator/compare/4.6.0...4.6.1) (2025-12-26)


### Bug Fixes

* Build Race Condition ([#244](https://github.com/pm7y/AzureEventGridSimulator/issues/244)) ([e07be4d](https://github.com/pm7y/AzureEventGridSimulator/commit/e07be4d9398ebfacbc1bb319442424a75ead4a6d))
* Fix Code Scanning Alerts ([#241](https://github.com/pm7y/AzureEventGridSimulator/issues/241)) ([f1d1463](https://github.com/pm7y/AzureEventGridSimulator/commit/f1d14630b377e115ddfe05242394d02bcae914cf))
* Release ([#243](https://github.com/pm7y/AzureEventGridSimulator/issues/243)) ([98e69a8](https://github.com/pm7y/AzureEventGridSimulator/commit/98e69a8f0d0918c2fce373cd5dbb3e7396abaeab))

## [4.6.0](https://github.com/pm7y/AzureEventGridSimulator/compare/4.5.0...4.6.0) (2025-12-26)


### Features

* Code formatting cleanup and Postman test reorganization ([#240](https://github.com/pm7y/AzureEventGridSimulator/issues/240)) ([79eca3d](https://github.com/pm7y/AzureEventGridSimulator/commit/79eca3dd4e28ccdd9b97c9d1cca81601490044f8))
* Enable null reference types ([#239](https://github.com/pm7y/AzureEventGridSimulator/issues/239)) ([08ab11e](https://github.com/pm7y/AzureEventGridSimulator/commit/08ab11e7d71e5ae3eadd47a4d4cd39dea147f4bb))


### Bug Fixes

* Remove DateTime in favour of TimeProvder and DateTimeOffset ([#238](https://github.com/pm7y/AzureEventGridSimulator/issues/238)) ([16ee484](https://github.com/pm7y/AzureEventGridSimulator/commit/16ee484ea5b4bc8025cbcd94d9256e63100211cf))


### Dependencies

* **actions:** bump the actions group with 5 updates ([#233](https://github.com/pm7y/AzureEventGridSimulator/issues/233)) ([346cfbb](https://github.com/pm7y/AzureEventGridSimulator/commit/346cfbbcaed7e1fbf7ab6abb748e8bae4dd496ff))

## [4.5.0](https://github.com/pm7y/AzureEventGridSimulator/compare/4.4.0...4.5.0) (2025-12-22)


### Features

* Dashboard MVP ([#231](https://github.com/pm7y/AzureEventGridSimulator/issues/231)) ([4559a3c](https://github.com/pm7y/AzureEventGridSimulator/commit/4559a3c5c40894c1eced0b2566b8526671d59420))

## [4.4.0](https://github.com/pm7y/AzureEventGridSimulator/compare/4.3.0...4.4.0) (2025-12-21)


### Features

* Add Event Hub Subscriber support ([#227](https://github.com/pm7y/AzureEventGridSimulator/issues/227)) ([c164b62](https://github.com/pm7y/AzureEventGridSimulator/commit/c164b620c87aba090b6a4566a2115c068221b006))


### Bug Fixes

* Code scanning alerts ([#228](https://github.com/pm7y/AzureEventGridSimulator/issues/228)) ([07bc08b](https://github.com/pm7y/AzureEventGridSimulator/commit/07bc08b6e2a0592c98d0a49de5b713d08344a40d))
* Docker.md path fix ([#223](https://github.com/pm7y/AzureEventGridSimulator/issues/223)) ([d732346](https://github.com/pm7y/AzureEventGridSimulator/commit/d732346179da606ac976ec9f37b331b664001bd8))

## [4.3.0](https://github.com/pm7y/AzureEventGridSimulator/compare/4.2.2...4.3.0) (2025-12-17)


### Features

* Add retry & dead letter support ([#220](https://github.com/pm7y/AzureEventGridSimulator/issues/220)) ([bff1bf1](https://github.com/pm7y/AzureEventGridSimulator/commit/bff1bf1fb32941840645061111736b9a56aa4d68))

## [4.2.2](https://github.com/pm7y/AzureEventGridSimulator/compare/4.2.1...4.2.2) (2025-12-17)


### Bug Fixes

* specify framework for publish in release workflow ([#217](https://github.com/pm7y/AzureEventGridSimulator/issues/217)) ([f7d4803](https://github.com/pm7y/AzureEventGridSimulator/commit/f7d48036111ba707de43b4348e32ccc06fb10b02))

## [4.2.1](https://github.com/pm7y/AzureEventGridSimulator/compare/4.2.0...4.2.1) (2025-12-17)


### Bug Fixes

* Trigger docker/nuget actions when release is published ([#215](https://github.com/pm7y/AzureEventGridSimulator/issues/215)) ([3e496ff](https://github.com/pm7y/AzureEventGridSimulator/commit/3e496ff1f6e1953351b91efca1f78db011d2d4b2))

## [4.2.0](https://github.com/pm7y/AzureEventGridSimulator/compare/4.1.2...4.2.0) (2025-12-17)


### Features

* Support for running as a 'dotnet tool' ([e12f849](https://github.com/pm7y/AzureEventGridSimulator/commit/e12f849c2a543165a86aaebe96a408d52199ca06))


### Dependencies

* **actions:** bump the actions group with 3 updates ([#205](https://github.com/pm7y/AzureEventGridSimulator/issues/205)) ([6ead0bc](https://github.com/pm7y/AzureEventGridSimulator/commit/6ead0bce10defc4c3b6f392adaed541924efe88f))
* **nuget:** Bump the minor-and-patch group with 8 updates ([#206](https://github.com/pm7y/AzureEventGridSimulator/issues/206)) ([5aecd6d](https://github.com/pm7y/AzureEventGridSimulator/commit/5aecd6d74b7094926fcfa7725f9da86ac301ed24))
