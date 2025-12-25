// 側邊欄選單控制
(function() {
    const sidebarToggle = document.getElementById('sidebarToggle');
    const sidebarMenu = document.getElementById('sidebarMenu');
    const sidebarOverlay = document.getElementById('sidebarOverlay');
    const sidebarClose = document.getElementById('sidebarClose');
    const body = document.body;

    function openSidebar() {
        sidebarMenu.classList.add('open');
        sidebarOverlay.classList.add('show');
        body.style.overflow = 'hidden';
    }

    function closeSidebar() {
        sidebarMenu.classList.remove('open');
        sidebarOverlay.classList.remove('show');
        body.style.overflow = '';
    }

    if (sidebarToggle) {
        sidebarToggle.addEventListener('click', function(e) {
            e.preventDefault();
            openSidebar();
        });
    }

    if (sidebarClose) {
        sidebarClose.addEventListener('click', function(e) {
            e.preventDefault();
            closeSidebar();
        });
    }

    if (sidebarOverlay) {
        sidebarOverlay.addEventListener('click', function() {
            closeSidebar();
        });
    }

    // 點擊側邊欄連結後自動關閉選單
    const sidebarLinks = document.querySelectorAll('.sidebar-nav-link');
    sidebarLinks.forEach(link => {
        link.addEventListener('click', function() {
            // 如果是表單按鈕，不關閉選單（讓表單提交）
            if (this.closest('form')) {
                return;
            }
            closeSidebar();
        });
    });
})();

// 免責條款同意機制
(function() {
    document.addEventListener('DOMContentLoaded', function() {
        const disclaimerAccepted = localStorage.getItem('disclaimerAccepted');
        const disclaimerModal = new bootstrap.Modal(document.getElementById('disclaimerModal'), {
            backdrop: 'static',
            keyboard: false
        });
        const disclaimerAcceptBtn = document.getElementById('disclaimerAccept');
        const mainContent = document.querySelector('main');
        const header = document.querySelector('header');
        const footer = document.querySelector('footer');

        if (!disclaimerAccepted) {
            // 顯示 Modal
            disclaimerModal.show();
            
            // 阻止背景操作
            if (mainContent) {
                mainContent.style.pointerEvents = 'none';
            }
            if (header) {
                header.style.pointerEvents = 'none';
            }
            if (footer) {
                footer.style.pointerEvents = 'none';
            }
        }

        if (disclaimerAcceptBtn) {
            disclaimerAcceptBtn.addEventListener('click', function() {
                localStorage.setItem('disclaimerAccepted', 'true');
                disclaimerModal.hide();
                
                // 恢復背景操作
                if (mainContent) {
                    mainContent.style.pointerEvents = '';
                }
                if (header) {
                    header.style.pointerEvents = '';
                }
                if (footer) {
                    footer.style.pointerEvents = '';
                }
            });
        }
    });
})();

// 時間相對格式轉換
function formatRelativeTime(dateString) {
    const date = new Date(dateString);
    const now = new Date();
    const diffMs = now - date;
    const diffSecs = Math.floor(diffMs / 1000);
    const diffMins = Math.floor(diffSecs / 60);
    const diffHours = Math.floor(diffMins / 60);
    const diffDays = Math.floor(diffHours / 24);
    const diffWeeks = Math.floor(diffDays / 7);
    const diffMonths = Math.floor(diffDays / 30);

    if (diffSecs < 60) {
        return '剛剛';
    } else if (diffMins < 60) {
        return diffMins + ' 分鐘前';
    } else if (diffHours < 24) {
        return diffHours + ' 小時前';
    } else if (diffDays < 7) {
        return diffDays + ' 天前';
    } else if (diffWeeks < 4) {
        return diffWeeks + ' 週前';
    } else if (diffMonths < 12) {
        return diffMonths + ' 個月前';
    } else {
        return Math.floor(diffMonths / 12) + ' 年前';
    }
}
