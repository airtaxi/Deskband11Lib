(() => {
    "use strict";

    const languageStorageKey = "deskband11lib-language";
    const rootElement = document.documentElement;
    const languageChangedListeners = [];

    const documentTexts = {
        en: {
            title: "Deskband11Lib: Host your own UI inside the Windows 11 taskbar",
            description: "Deskband11Lib is a .NET library that hosts WinUI 3 and WPF content inside the Windows 11 taskbar and fits it into the free space between the app buttons and the notification area."
        },
        ko: {
            title: "Deskband11Lib: Windows 11 작업 표시줄 안에 직접 만든 UI를 띄워 보세요",
            description: "Deskband11Lib는 WinUI 3와 WPF 콘텐츠를 Windows 11 작업 표시줄 안에 배치하고, 앱 버튼과 알림 영역 사이에 남는 공간에 맞춰 크기를 조정하는 .NET 라이브러리입니다."
        }
    };

    const installCommands = {
        winui: "dotnet add package Deskband11Lib.WinUI",
        wpf: "dotnet add package Deskband11Lib.Wpf"
    };

    const getCurrentLanguage = () => (rootElement.dataset.lang === "ko" ? "ko" : "en");
    const localize = (englishText, koreanText) => (getCurrentLanguage() === "ko" ? koreanText : englishText);

    function applyLanguage(language, shouldPersist) {
        rootElement.dataset.lang = language;
        rootElement.lang = language;
        document.title = documentTexts[language].title;
        document.querySelector('meta[name="description"]')?.setAttribute("content", documentTexts[language].description);

        for (const button of document.querySelectorAll("[data-set-language]")) button.setAttribute("aria-pressed", String(button.dataset.setLanguage === language));
        for (const element of document.querySelectorAll("[data-label-en]")) element.setAttribute("aria-label", language === "ko" ? element.dataset.labelKo : element.dataset.labelEn);
        for (const element of document.querySelectorAll("[data-alt-en]")) element.setAttribute("alt", language === "ko" ? element.dataset.altKo : element.dataset.altEn);

        if (shouldPersist) {
            try { localStorage.setItem(languageStorageKey, language); }
            catch { /* Storage can be unavailable in private browsing. */ }
        }

        for (const listener of languageChangedListeners) listener();
    }

    function applyFramework(framework) {
        for (const button of document.querySelectorAll("[data-set-framework]")) button.setAttribute("aria-pressed", String(button.dataset.setFramework === framework));
        for (const panel of document.querySelectorAll("[data-framework-panel]")) panel.hidden = panel.dataset.frameworkPanel !== framework;

        const installCommandElement = document.getElementById("install-command");
        if (installCommandElement) installCommandElement.textContent = installCommands[framework];
    }

    function initializeCopyButtons() {
        for (const button of document.querySelectorAll("[data-copy-target]")) {
            let resetTimer = 0;
            button.addEventListener("click", async () => {
                const targetElement = document.getElementById(button.dataset.copyTarget);
                if (!targetElement) return;

                try {
                    await navigator.clipboard.writeText(targetElement.textContent.trim());
                    button.dataset.state = "copied";
                }
                catch {
                    return;
                }

                window.clearTimeout(resetTimer);
                resetTimer = window.setTimeout(() => delete button.dataset.state, 1600);
            });
        }
    }

    function createDiagramElement(parentElement, className) {
        const element = document.createElement("div");
        element.className = className;
        parentElement.append(element);
        return element;
    }

    function setHorizontalSpan(element, left, right, totalWidth) {
        element.style.left = `${(left / totalWidth) * 100}%`;
        element.style.width = `${(Math.max(0, right - left) / totalWidth) * 100}%`;
    }

    // Mirrors the gap selection in TaskbarLayoutCalculator for a fixed example taskbar.
    function initializePlacementDemo(container) {
        const taskbarWidth = 1920;
        const contentWidth = 360;
        const layouts = {
            center: {
                widgets: { left: 0, right: 160 },
                buttons: { left: 740, right: 1180 },
                notificationArea: { left: 1600, right: 1920 }
            },
            left: {
                widgets: { left: 1440, right: 1600 },
                buttons: { left: 0, right: 440 },
                notificationArea: { left: 1600, right: 1920 }
            }
        };

        const state = { alignment: "center", placement: "Auto" };
        const barElement = container.querySelector("[data-diagram-bar]");
        const measuresElement = container.querySelector("[data-diagram-measures]");
        const captionElement = container.querySelector("[data-diagram-caption]");

        const widgetsElement = createDiagramElement(barElement, "diagram-block");
        const buttonsElement = createDiagramElement(barElement, "diagram-block");
        const notificationAreaElement = createDiagramElement(barElement, "diagram-block");
        const contentElement = createDiagramElement(barElement, "diagram-block diagram-content");
        const leftMeasureElement = createDiagramElement(measuresElement, "measure");
        const rightMeasureElement = createDiagramElement(measuresElement, "measure");
        const leftMeasureLabel = document.createElement("span");
        const rightMeasureLabel = document.createElement("span");
        leftMeasureElement.append(leftMeasureLabel);
        rightMeasureElement.append(rightMeasureLabel);

        for (const groupElement of container.querySelectorAll("[data-placement-control]")) {
            groupElement.addEventListener("click", event => {
                const button = event.target.closest("button[data-value]");
                if (!button) return;

                state[groupElement.dataset.placementControl] = button.dataset.value;
                for (const siblingButton of groupElement.querySelectorAll("button[data-value]")) siblingButton.setAttribute("aria-pressed", String(siblingButton === button));
                render();
            });
        }

        function describePlacement(leftGapWidth, rightGapWidth) {
            const placementCode = `<code>${state.placement}</code>`;
            if (state.alignment === "left") {
                if (state.placement === "BeforeNotificationArea") return localize("On a left-aligned taskbar the Widgets button sits next to the notification area, so the content ends right before the Widgets button.", "왼쪽 정렬 작업 표시줄에서는 위젯 버튼이 알림 영역 옆에 있으므로, 콘텐츠가 위젯 버튼 바로 왼쪽에서 끝납니다.");
                if (state.placement === "Auto") return localize(`${placementCode} picks the wider gap. On a left-aligned taskbar that is always the gap before the notification area, so the content ends right before the Widgets button.`, `${placementCode} 값은 더 넓은 공간을 고릅니다. 왼쪽 정렬 작업 표시줄에서는 항상 알림 영역 앞의 공간이 선택되므로, 콘텐츠가 위젯 버튼 바로 왼쪽에서 끝납니다.`);
                return localize(`A left-aligned taskbar has no gap left of the Start button, so ${placementCode} falls back to <code>BeforeNotificationArea</code>.`, `왼쪽 정렬 작업 표시줄에는 시작 버튼 왼쪽에 공간이 없으므로, ${placementCode} 값은 <code>BeforeNotificationArea</code>처럼 동작합니다.`);
            }

            switch (state.placement) {
                case "Auto":
                    return localize(`The gap left of the Start button (${leftGapWidth} px) is wider than the gap before the notification area (${rightGapWidth} px), so ${placementCode} behaves like <code>LeftEdge</code> and the content starts right after the Widgets button.`, `시작 버튼 왼쪽 공간(${leftGapWidth}px)이 알림 영역 앞 공간(${rightGapWidth}px)보다 넓으므로, ${placementCode} 값은 <code>LeftEdge</code>처럼 동작하여 콘텐츠가 위젯 버튼 바로 오른쪽에서 시작합니다.`);
                case "LeftEdge":
                    return localize("The content starts right after the Widgets button, at the far left of the free space.", "콘텐츠가 위젯 버튼 바로 오른쪽, 남는 공간의 가장 왼쪽에서 시작합니다.");
                case "BeforeStartButton":
                    return localize("The content ends right before the Start button.", "콘텐츠가 시작 버튼 바로 왼쪽에서 끝납니다.");
                default:
                    return localize("The content ends right before the notification area.", "콘텐츠가 알림 영역 바로 왼쪽에서 끝납니다.");
            }
        }

        function updateMeasure(measureElement, labelElement, gap, gapWidth, isSelected) {
            measureElement.hidden = gapWidth <= 0;
            measureElement.classList.toggle("is-selected", isSelected);
            setHorizontalSpan(measureElement, gap.left, gap.right, taskbarWidth);
            labelElement.textContent = `${gapWidth} px`;
        }

        function render() {
            const layout = layouts[state.alignment];
            const isCentered = state.alignment === "center";
            const leftGap = { left: layout.widgets.right, right: layout.buttons.left };
            const rightGap = { left: layout.buttons.right, right: layout.notificationArea.left };
            if (layout.widgets.left >= rightGap.left && layout.widgets.left < rightGap.right) rightGap.right = layout.widgets.left;

            const leftGapWidth = Math.max(0, leftGap.right - leftGap.left);
            const rightGapWidth = Math.max(0, rightGap.right - rightGap.left);
            const usesLeftGap = state.placement === "LeftEdge" || state.placement === "BeforeStartButton" ? isCentered : state.placement === "Auto" && isCentered && leftGapWidth > rightGapWidth;
            const selectedGap = usesLeftGap ? leftGap : rightGap;
            const selectedGapWidth = usesLeftGap ? leftGapWidth : rightGapWidth;
            const startsAtLeftEdge = usesLeftGap && (state.placement === "LeftEdge" || state.placement === "Auto");
            const width = Math.min(contentWidth, selectedGapWidth);
            const contentLeft = startsAtLeftEdge ? selectedGap.left : selectedGap.right - width;

            setHorizontalSpan(widgetsElement, layout.widgets.left, layout.widgets.right, taskbarWidth);
            setHorizontalSpan(buttonsElement, layout.buttons.left, layout.buttons.right, taskbarWidth);
            setHorizontalSpan(notificationAreaElement, layout.notificationArea.left, layout.notificationArea.right, taskbarWidth);
            setHorizontalSpan(contentElement, contentLeft, contentLeft + width, taskbarWidth);

            widgetsElement.textContent = localize("Widgets", "위젯");
            buttonsElement.textContent = localize("Start and app buttons", "시작 버튼과 앱 버튼");
            notificationAreaElement.textContent = localize("Notification area", "알림 영역");
            contentElement.textContent = localize("Your content", "내 콘텐츠");

            updateMeasure(leftMeasureElement, leftMeasureLabel, leftGap, leftGapWidth, usesLeftGap);
            updateMeasure(rightMeasureElement, rightMeasureLabel, rightGap, rightGapWidth, !usesLeftGap);
            captionElement.innerHTML = describePlacement(leftGapWidth, rightGapWidth);
        }

        languageChangedListeners.push(render);
        render();
    }

    // Mirrors the Fixed-then-Stretch allocation in TaskbarLayoutCalculator at 100% scaling.
    function initializeInstancesDemo(container) {
        const maximumAvailableWidth = 1000;
        const notificationAreaWidth = 240;
        const totalWidth = maximumAvailableWidth + notificationAreaWidth;
        const instances = [
            { key: "media", englishName: "Media", koreanName: "미디어", preferredWidth: 320, manualSlotPriority: 65535, arrivalIndex: 0 },
            { key: "build", englishName: "Build status", koreanName: "빌드 상태", preferredWidth: Number.POSITIVE_INFINITY, manualSlotPriority: 65535, arrivalIndex: 1 },
            { key: "timer", englishName: "Timer", koreanName: "타이머", preferredWidth: 200, manualSlotPriority: 100, arrivalIndex: 2 }
        ];

        const isStretch = instance => !Number.isFinite(instance.preferredWidth);
        const orderedInstances = [...instances].sort((first, second) => first.manualSlotPriority - second.manualSlotPriority || first.arrivalIndex - second.arrivalIndex);
        const fixedInstances = orderedInstances.filter(instance => !isStretch(instance));
        const stretchInstances = orderedInstances.filter(isStretch);
        const fixedWidthTotal = fixedInstances.reduce((sum, instance) => sum + instance.preferredWidth, 0);

        const widthInputElement = container.querySelector("[data-instances-width]");
        const widthOutputElement = container.querySelector("[data-instances-width-output]");
        const resizeInputElement = container.querySelector("[data-instances-resize]");
        const barElement = container.querySelector("[data-instances-bar]");
        const captionElement = container.querySelector("[data-instances-caption]");
        const tableBodyElement = container.querySelector("[data-instances-table]");

        const appButtonsElement = createDiagramElement(barElement, "diagram-block");
        const areaElement = createDiagramElement(barElement, "diagram-area");
        const slotElements = new Map(orderedInstances.map(instance => [instance.key, createDiagramElement(barElement, isStretch(instance) ? "diagram-block diagram-slot diagram-stretch" : "diagram-block diagram-slot diagram-content")]));
        const notificationAreaElement = createDiagramElement(barElement, "diagram-block");

        function allocate(availableWidth, allowFixedSlotResize) {
            const allocations = new Map();
            let allocatedWidthSum = 0;

            const nonResizableFixedInstances = allowFixedSlotResize ? [] : fixedInstances;
            const resizableFixedInstances = allowFixedSlotResize ? fixedInstances : [];

            for (const instance of nonResizableFixedInstances) {
                const width = Math.min(instance.preferredWidth, Math.max(0, availableWidth - allocatedWidthSum));
                allocations.set(instance.key, width);
                allocatedWidthSum += width;
            }

            const remainingForResizable = Math.max(0, availableWidth - allocatedWidthSum);
            const resizableDesiredWidthSum = resizableFixedInstances.reduce((sum, instance) => sum + instance.preferredWidth, 0);
            if (resizableFixedInstances.length > 0) {
                if (remainingForResizable >= resizableDesiredWidthSum) {
                    for (const instance of resizableFixedInstances) allocations.set(instance.key, instance.preferredWidth);
                    allocatedWidthSum += resizableDesiredWidthSum;
                }
                else {
                    let distributedWidth = 0;
                    for (const instance of resizableFixedInstances) {
                        const width = Math.floor((remainingForResizable * instance.preferredWidth) / resizableDesiredWidthSum);
                        allocations.set(instance.key, width);
                        distributedWidth += width;
                    }

                    let leftoverWidth = remainingForResizable - distributedWidth;
                    for (const instance of resizableFixedInstances) {
                        if (leftoverWidth <= 0) break;
                        allocations.set(instance.key, allocations.get(instance.key) + 1);
                        leftoverWidth--;
                    }

                    allocatedWidthSum += remainingForResizable;
                }
            }

            const stretchWidth = stretchInstances.length > 0 ? Math.floor(Math.max(0, availableWidth - allocatedWidthSum) / stretchInstances.length) : 0;
            for (const instance of stretchInstances) allocations.set(instance.key, stretchWidth);
            return allocations;
        }

        function describeAllocation(availableWidth, allowFixedSlotResize, allocations) {
            const stretchWidth = allocations.get("build");
            const timerWidth = allocations.get("timer");
            const mediaWidth = allocations.get("media");

            if (availableWidth >= fixedWidthTotal) {
                if (stretchWidth <= 0) return localize("Both Fixed instances get their full <code>PreferredWidth</code>, which leaves nothing for Build status.", "두 Fixed 인스턴스가 <code>PreferredWidth</code>만큼 너비를 모두 받고 나면 남는 공간이 없으므로, 빌드 상태에는 너비가 할당되지 않습니다.");
                return localize(`Both Fixed instances get their full <code>PreferredWidth</code>, and Build status stretches into the remaining ${stretchWidth} px.`, `두 Fixed 인스턴스가 <code>PreferredWidth</code>만큼 너비를 모두 받고, 빌드 상태는 남은 너비(${stretchWidth}px)만큼 늘어납니다.`);
            }

            if (allowFixedSlotResize) return localize(`The Fixed instances ask for ${fixedWidthTotal} px in total but only ${availableWidth} px is free, so both shrink in proportion to their <code>PreferredWidth</code>. Build status gets no width.`, `남은 공간(${availableWidth}px)이 Fixed 인스턴스들이 요청한 너비의 합계(${fixedWidthTotal}px)보다 좁으므로, 두 인스턴스 모두 <code>PreferredWidth</code> 비율에 맞춰 줄어듭니다. 빌드 상태에는 너비가 할당되지 않습니다.`);
            if (mediaWidth > 0) return localize(`Timer goes first because of <code>ManualSlotPriority = 100</code> and gets its full ${timerWidth} px. Media gets the remaining ${mediaWidth} px, and Build status gets no width.`, `타이머는 <code>ManualSlotPriority = 100</code>이므로 가장 먼저 요청한 너비(${timerWidth}px)를 모두 받습니다. 미디어는 남은 너비(${mediaWidth}px)만 받고, 빌드 상태에는 너비가 할당되지 않습니다.`);
            return localize(`Timer goes first and takes all ${timerWidth} px, so Media and Build status get no width.`, `타이머가 가장 먼저 남은 너비(${timerWidth}px)를 모두 차지하므로, 미디어와 빌드 상태에는 너비가 할당되지 않습니다.`);
        }

        function render() {
            const availableWidth = Number(widthInputElement.value);
            const allowFixedSlotResize = resizeInputElement.checked;
            const allocations = allocate(availableWidth, allowFixedSlotResize);
            const areaLeft = maximumAvailableWidth - availableWidth;

            widthOutputElement.textContent = `${availableWidth} px`;
            appButtonsElement.hidden = areaLeft <= 0;
            appButtonsElement.textContent = localize("App buttons", "앱 버튼");
            notificationAreaElement.textContent = localize("Notification area", "알림 영역");
            setHorizontalSpan(appButtonsElement, 0, areaLeft, totalWidth);
            setHorizontalSpan(areaElement, areaLeft, maximumAvailableWidth, totalWidth);
            setHorizontalSpan(notificationAreaElement, maximumAvailableWidth, totalWidth, totalWidth);

            let offset = 0;
            for (const instance of orderedInstances) {
                const width = allocations.get(instance.key);
                const right = maximumAvailableWidth - offset;
                const slotElement = slotElements.get(instance.key);
                const name = localize(instance.englishName, instance.koreanName);
                setHorizontalSpan(slotElement, right - width, right, totalWidth);
                slotElement.textContent = name;
                slotElement.title = `${name}: ${width} px`;
                slotElement.classList.toggle("is-empty", width <= 0);
                offset += width;
            }

            tableBodyElement.replaceChildren(...orderedInstances.map(instance => {
                const rowElement = document.createElement("tr");
                const preferredWidthText = isStretch(instance) ? "double.MaxValue" : String(instance.preferredWidth);
                rowElement.innerHTML = `<td>${localize(instance.englishName, instance.koreanName)}</td><td>${isStretch(instance) ? "Stretch" : "Fixed"}</td><td><code>${preferredWidthText}</code></td><td><code>${instance.manualSlotPriority}</code></td><td class="num">${allocations.get(instance.key)} px</td>`;
                return rowElement;
            }));

            captionElement.innerHTML = describeAllocation(availableWidth, allowFixedSlotResize, allocations);
        }

        widthInputElement.addEventListener("input", render);
        resizeInputElement.addEventListener("change", render);
        languageChangedListeners.push(render);
        render();
    }

    for (const button of document.querySelectorAll("[data-set-language]")) button.addEventListener("click", () => applyLanguage(button.dataset.setLanguage, true));
    for (const button of document.querySelectorAll("[data-set-framework]")) button.addEventListener("click", () => applyFramework(button.dataset.setFramework));

    initializeCopyButtons();

    const placementDemoElement = document.getElementById("placement-demo");
    if (placementDemoElement) initializePlacementDemo(placementDemoElement);

    const instancesDemoElement = document.getElementById("instances-demo");
    if (instancesDemoElement) initializeInstancesDemo(instancesDemoElement);

    applyLanguage(getCurrentLanguage(), false);
})();
