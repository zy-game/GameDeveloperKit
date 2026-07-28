---
type: decision
title: 云连接按 Provider 独立保存
tags:
  - cloud
  - cos
  - oss
  - hls
keywords:
  - CloudProjectConfig
  - Provider
  - Region
  - Catalog
date: '2026-07-28'
source: ''
status: active
related: []
---

腾讯 COS 与阿里云 OSS 分别保存 Profile、Bucket、Region、Endpoint、RootPrefix、PublicBaseUrl；当前 Provider 是媒体库唯一存储源。Provider/Region 不匹配必须在生成 Catalog URL 或发起云 HTTP 前失败。旧扁平连接字段不做兼容迁移。