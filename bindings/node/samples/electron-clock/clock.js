const timeElement = document.getElementById('time');
const dateElement = document.getElementById('date');

function pad(value) {
    return String(value).padStart(2, '0');
}

// A left or right taskbar gives the window a portrait shape, which only fits the short formats.
function refresh() {
    const now = new Date();
    const isTaskbarVertical = window.innerHeight > window.innerWidth;
    const hours = pad(now.getHours());
    const minutes = pad(now.getMinutes());
    const month = pad(now.getMonth() + 1);
    const day = pad(now.getDate());
    timeElement.textContent = isTaskbarVertical ? `${hours}:${minutes}` : `${hours}:${minutes}:${pad(now.getSeconds())}`;
    dateElement.textContent = isTaskbarVertical ? `${month}-${day}` : `${now.getFullYear()}-${month}-${day}`;
}

refresh();
setInterval(refresh, 1000);
window.addEventListener('resize', refresh);
