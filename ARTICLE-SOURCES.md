# 离线英语阅读库：来源与筛选说明

本软件随包提供 200 篇可离线阅读的英文材料，生成日期为 2026-07-28。
文章正文以语义化 HTML（段落、小标题、列表及可点击词元）保存，分类字段和
逐词词表也均保存在本地，阅读时不需要网络。

## 来源与许可

| 来源 | 数量 | 离线保存方式 | 许可检查 |
| --- | ---: | --- | --- |
| VOA Learning English | 188 | 原创文章正文 | VOA 明确说明 Learning English 自有文本属于公有领域；构建器会排除正文中出现 Associated Press、Reuters 或 AFP 来源标记的稿件 |
| Nature Portfolio | 12 | 开放获取论文的英文摘要精读 | 逐篇从 Crossref 元数据核对 Creative Commons 许可；每篇在软件内保留作者、DOI 来源链接和许可链接 |

VOA 使用政策：
<https://learningenglish.voanews.com/p/6861.html>

Nature 开放获取内容说明：
<https://support.nature.com/en/support/solutions/articles/6000214239-use-of-an-open-access-article>

每篇文章的标题、作者、发布日期、原始链接、许可说明和 HTML 正文都保存在
`resources/offline-articles.json.gz` 中，可独立审计。Nature 材料保留的是适合
短篇精读的开放获取摘要，不把收费新闻或未授权的 Nature 正文复制进软件。

## 分类结果

- 难度：基础 131 篇、进阶 48 篇、挑战 21 篇。
- 长度：短篇 58 篇、中篇 120 篇、长篇 22 篇。
- 主题：人与自我/健康生活、人与社会/教育成长/社会沟通/文化艺术、
  人与自然/科技创新等。
- 全库约 11.3 万词；去重后 7,767 个英文词形全部进入随包 ECDICT 本地词典。

难度由句长和长词比例做一致性分级，长度按正文词数分级；它们是阅读选材提示，
不是考试分数预测。

## 与高考趋势的关系

选材参照近年考试院公开评析所强调的三大主题语境“人与自我、人与社会、
人与自然”，并优先保留生活健康、科技创新、环境保护、教育成长、人际沟通和
文化理解等内容。完整高考真题没有从培训网站或扫描站转载：目前找到的权威
考试院页面主要是命题评析，不是附带再分发许可的完整试卷。

## 重新构建

```powershell
python tools\build-offline-article-library.py
python tools\build-ecdict-subset.py <ECDICT.csv> `
  resources\ecdict-learning.tsv.gz `
  --word-list resources\article-vocabulary.txt
```

第一条命令联网抓取并执行来源、年份、长度和通讯社署名过滤；正常的软件构建只
复制已经审计并签入仓库的压缩文章库，不会临时联网。
