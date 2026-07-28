---
type: learning
title: Android AVPro DecodeFailed 与 HLS 双播放器升级
tags:
  - AVProVideo
  - Android
  - HLS
  - MediaCodec
keywords:
  - DecodeFailed
  - allowUnsupportedVideoTrackVariants
  - preload
  - 2K
  - dual-player
date: '2026-07-28'
source: ''
status: active
related: []
---

当前 HLS 最高档为 2560x1440 H.264 High Level 5.0。VideoPlayableHandle 在 Android 开启 allowUnsupportedVideoTrackVariants，并在预加载转正式播放时创建 preferHighBitrate 候选播放器，导致 240p 源播放器与最高码率候选并发持有硬解码器；暂停的批量预加载播放器也继续占用资源。优先关闭 unsupported variants、Android 限制最高 1080p，并改为同一 ExoPlayer 动态释放启动分辨率限制。AVPro generic 4K 文案不是素材实际分辨率证据。