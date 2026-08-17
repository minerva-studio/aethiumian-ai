/**
 * Keeps Material search results within the language of the current page.
 */
function filterSearchResultsByLanguage() {
  const resultList = document.querySelector(".md-search-result__list");
  const resultMeta = document.querySelector(".md-search-result__meta");
  if (!resultList || !resultMeta) {
    return;
  }

  const isChinesePage = document.documentElement.lang.toLowerCase().startsWith("zh");
  const resultItems = resultList.querySelectorAll(":scope > .md-search-result__item");
  let visibleResultCount = 0;

  for (const item of resultItems) {
    const link = item.querySelector(".md-search-result__link");
    if (!link) {
      continue;
    }

    const resultPath = new URL(link.href, window.location.href).pathname;
    const isChineseResult = resultPath.includes("/zh/");
    item.hidden = isChinesePage !== isChineseResult;
    if (!item.hidden) {
      visibleResultCount += item.querySelectorAll(".md-search-result__link").length;
    }
  }

  if (resultItems.length === 0) {
    return;
  }

  let resultMessage;
  if (isChinesePage) {
    resultMessage = visibleResultCount === 0
      ? "没有找到符合条件的结果"
      : `找到 ${visibleResultCount} 个符合条件的结果`;
  } else {
    resultMessage = visibleResultCount === 0
      ? "No matching documents"
      : `${visibleResultCount} matching document${visibleResultCount === 1 ? "" : "s"}`;
  }

  if (resultMeta.textContent !== resultMessage) {
    resultMeta.textContent = resultMessage;
  }
}

/**
 * Observes Material's asynchronous search rendering and filters each update.
 */
function observeSearchResults() {
  const searchResult = document.querySelector("[data-md-component='search-result']");
  if (!searchResult) {
    return;
  }

  const observer = new MutationObserver(filterSearchResultsByLanguage);
  observer.observe(searchResult, { childList: true, subtree: true });
  filterSearchResultsByLanguage();
}

document.addEventListener("DOMContentLoaded", observeSearchResults);
