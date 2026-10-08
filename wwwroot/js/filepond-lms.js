document.querySelectorAll("[data-filepond]").forEach((input) => {
    const hiddenIds = document.getElementById(input.dataset.idsTarget);
    const form = input.closest("form");
    const token = form?.querySelector('input[name="__RequestVerificationToken"]')?.value;

    if (!hiddenIds || !token) {
        return;
    }

    const getIds = () => hiddenIds.value.split(",").map((id) => id.trim()).filter(Boolean);
    const setIds = (ids) => {
        hiddenIds.value = [...new Set(ids)].join(",");
    };

    FilePond.create(input, {
        allowMultiple: true,
        maxFiles: 5,
        labelIdle: 'Kéo thả tệp hoặc <span class="filepond--label-action">chọn từ thiết bị</span>',
        server: {
            process: {
                url: input.dataset.processUrl,
                method: "POST",
                headers: { RequestVerificationToken: token },
                onload: (response) => response.trim(),
                onerror: (response) => response
            },
            revert: {
                url: input.dataset.revertUrl,
                method: "DELETE",
                headers: { RequestVerificationToken: token }
            }
        },
        onprocessfile: (error, item) => {
            if (!error && item.serverId) {
                setIds([...getIds(), item.serverId]);
            }
        },
        onremovefile: (_error, item) => {
            if (item.serverId) {
                setIds(getIds().filter((id) => id !== item.serverId));
            }
        }
    });
});

document.querySelectorAll("[data-remove-uploaded-file]").forEach((button) => {
    button.addEventListener("click", () => {
        const hiddenIds = document.getElementById(button.dataset.idsTarget);
        if (!hiddenIds) {
            return;
        }

        const fileId = button.dataset.removeUploadedFile;
        hiddenIds.value = hiddenIds.value.split(",")
            .map((id) => id.trim())
            .filter((id) => id && id !== fileId)
            .join(",");
        button.closest("[data-uploaded-file]")?.remove();
    });
});
