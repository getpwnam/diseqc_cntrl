# ---------- Stage 1: Download toolchains ----------
FROM ubuntu:24.04 AS downloader

ENV DEBIAN_FRONTEND=noninteractive

RUN apt-get update && apt-get install -y --no-install-recommends \
    curl xz-utils unzip wget ca-certificates \
    && rm -rf /var/lib/apt/lists/*

WORKDIR /tmp

# ---- GCC ARM Toolchain ----
ARG GCC_VERSION=15.2.rel1
ARG GCC_URI=https://developer.arm.com/-/media/Files/downloads/gnu/${GCC_VERSION}/binrel/arm-gnu-toolchain-${GCC_VERSION}-x86_64-arm-none-eabi.tar.xz

RUN mkdir -p /tmp/gcc \
    && curl -fL -o gcc.tar.xz ${GCC_URI} \
    && tar -xJf gcc.tar.xz -C /tmp/gcc --strip-components=1 \
    && rm -rf /tmp/gcc/share/doc /tmp/gcc/share/gcc-arm-none-eabi/samples

# ---- CMake ----
ARG CMAKE_VERSION=3.31.6
ARG CMAKE_SCRIPT=https://github.com/Kitware/CMake/releases/download/v${CMAKE_VERSION}/cmake-${CMAKE_VERSION}-linux-x86_64.sh

RUN mkdir -p /tmp/cmake \
    && curl -fL -o cmake.sh ${CMAKE_SCRIPT} \
    && chmod +x cmake.sh \
    && ./cmake.sh --skip-license --prefix=/tmp/cmake \
    && rm cmake.sh


# ---------- Stage 2: Dev container ----------
FROM python:3.11-slim AS devcontainer

ENV DEBIAN_FRONTEND=noninteractive

# ---- Base packages ----
RUN apt-get update && apt-get install -y --no-install-recommends \
    git \
    curl \
    wget \
    sudo \
    clang-format \
    ninja-build \
    stlink-tools \
    srecord \
    ca-certificates \
    gnupg


# ---- Local packages ----
RUN apt-get install -y --no-install-recommends \
    libicu-dev \
    usbutils \
    ripgrep \
    less \
    vim \
    nano \
    binutils \
    bash-completion \
    openssh-client

# ---- GitHub CLI ----
RUN mkdir -p -m 755 /etc/apt/keyrings \
	&& out=$(mktemp) && wget -nv -O$out https://cli.github.com/packages/githubcli-archive-keyring.gpg \
	&& cat $out | tee /etc/apt/keyrings/githubcli-archive-keyring.gpg > /dev/null \
	&& chmod go+r /etc/apt/keyrings/githubcli-archive-keyring.gpg \
	&& mkdir -p -m 755 /etc/apt/sources.list.d \
	&& echo "deb [arch=$(dpkg --print-architecture) signed-by=/etc/apt/keyrings/githubcli-archive-keyring.gpg] https://cli.github.com/packages stable main" | tee /etc/apt/sources.list.d/github-cli.list > /dev/null \
	&& apt update \
	&& apt install gh -y

# ---- Clean up ----
RUN rm -rf /var/lib/apt/lists/*

# ---- Install .NET SDK (includes NuGet, MSBuild) ----
RUN curl -fsSL https://dot.net/v1/dotnet-install.sh -o dotnet-install.sh \
    && chmod +x dotnet-install.sh \
    && ./dotnet-install.sh --channel 8.0 --install-dir /usr/share/dotnet \
    && ln -s /usr/share/dotnet/dotnet /usr/bin/dotnet \
    && rm dotnet-install.sh

# ---- Create non-root user ----
ARG USERNAME=vscode
ARG USER_UID=1000
ARG USER_GID=1000

RUN groupadd --gid ${USER_GID} ${USERNAME} \
    && useradd --uid ${USER_UID} --gid ${USER_GID} -m -s /bin/bash ${USERNAME} \
    && echo "${USERNAME} ALL=(ALL) NOPASSWD:ALL" > /etc/sudoers.d/${USERNAME} \
    && chmod 0440 /etc/sudoers.d/${USERNAME}

# ---- Copy toolchains from downloader ----
RUN mkdir -p /usr/local/gcc /opt/cmake

COPY --from=downloader /tmp/gcc /usr/local/gcc
COPY --from=downloader /tmp/cmake /opt/cmake

ENV PATH="/opt/cmake/bin:/usr/local/gcc/bin:${PATH}"
ENV ARM_GCC_PATH=/usr/local/gcc/bin

# ---- Fix MSBuild compatibility ----
RUN ln -sf /usr/bin/dotnet /usr/local/bin/msbuild \
    && ln -sf /usr/bin/dotnet /usr/local/bin/xbuild

# ---- Install nanoFramework tools ----
RUN mkdir -p /usr/local/share/dotnet-tools \
    && dotnet tool install --tool-path /usr/local/share/dotnet-tools nanoff \
    && ln -sf /usr/local/share/dotnet-tools/nanoff /usr/local/bin/nanoff

# ---- hex2dfu ----
ENV HEX2DFU_PATH=/usr/local/bin/hex2dfu

RUN mkdir -p ${HEX2DFU_PATH} \
    && curl -L https://github.com/nanoframework/hex2dfu/releases/latest/download/hex2dfu \
       -o ${HEX2DFU_PATH}/hex2dfu \
    && chmod +x ${HEX2DFU_PATH}/hex2dfu

# ---- Python deps ----
RUN pip install --no-cache-dir pyserial "kconfiglib>=14.1.0"

# ---- Source repos ----
WORKDIR /workspace

RUN git clone --depth 1 --branch stable-21.11.x https://github.com/chibios-upstream/chibios.git sources/ChibiOS \
 && git clone --depth 1 --branch chibios-21.11.x https://github.com/ChibiOS/ChibiOS-Contrib.git sources/ChibiOS-Contrib

# STM32 HAL
#RUN git clone --depth 1 --branch v1.7.8 https://github.com/STMicroelectronics/stm32f0xx_hal_driver.git sources/stm32f0xx_hal_driver
RUN git clone --depth 1 --branch v1.8.5 https://github.com/STMicroelectronics/stm32f4xx_hal_driver.git sources/stm32f4xx_hal_driver
#RUN git clone --depth 1 --branch v1.3.3 https://github.com/STMicroelectronics/stm32f7xx_hal_driver.git sources/stm32f7xx_hal_driver
#RUN git clone --depth 1 --branch v1.11.6 https://github.com/STMicroelectronics/stm32h7xx_hal_driver.git sources/stm32h7xx_hal_driver
#RUN git clone --depth 1 --branch v1.13.6 https://github.com/STMicroelectronics/stm32l4xx_hal_driver.git sources/stm32l4xx_hal_driver

# CMSIS
# RUN git clone --depth 1 --branch v2.3.7 https://github.com/STMicroelectronics/cmsis_device_f0.git sources/cmsis_device_f0 \
#  && git clone --depth 1 --branch v2.6.11 https://github.com/STMicroelectronics/cmsis_device_f4.git sources/cmsis_device_f4 \
#  && git clone --depth 1 --branch v1.2.10 https://github.com/STMicroelectronics/cmsis_device_f7.git sources/cmsis_device_f7 \
#  && git clone --depth 1 --branch v1.10.7 https://github.com/STMicroelectronics/cmsis_device_h7.git sources/cmsis_device_h7 \
#  && git clone --depth 1 --branch v1.7.5 https://github.com/STMicroelectronics/cmsis_device_l4.git sources/cmsis_device_l4 \
#  && git clone --depth 1 --branch v5.9.0_20250520 https://github.com/STMicroelectronics/cmsis_core.git sources/cmsis_core

# Dependencies
# RUN git clone --depth 1 --branch R0.16 https://github.com/abbrev/fatfs.git sources/fatfs \
#  && git clone --depth 1 --branch v2.11.2 https://github.com/littlefs-project/littlefs sources/littlefs \
#  && git clone --depth 1 --branch STABLE-2_1_3_RELEASE https://github.com/lwip-tcpip/lwip.git sources/lwip \
#  && git clone --depth 1 --branch mbedtls-3.6.5 https://github.com/ARMmbed/mbedtls.git sources/mbedtls \
#  && cd sources/mbedtls && git submodule update --init --recursive

# ---- Final cleanup ----
RUN apt-get autoremove -y && apt-get clean

# ---- Customize bash prompt to show git branch and dirty state ----
RUN cat << 'EOF' >> /home/vscode/.bashrc
# Git prompt helpers
parse_git_branch() { git branch --show-current 2>/dev/null; }
parse_git_dirty() { [[ $(git status --porcelain 2>/dev/null) ]] && echo "*"; }

export PS1="\[\e[1;36m\]\u@\h\[\e[0m\]:\[\e[1;33m\]\w\[\e[0;32m\]\$(git rev-parse --is-inside-work-tree >/dev/null 2>&1 && echo \" (\$(parse_git_branch)\$(parse_git_dirty))\")\[\e[0m\]\$ "
EOF

ENV DEBIAN_FRONTEND=dialog

USER ${USERNAME}