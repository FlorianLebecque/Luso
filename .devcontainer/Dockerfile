FROM mcr.microsoft.com/dotnet/sdk:8.0

ENV DEBIAN_FRONTEND=noninteractive

RUN apt-get update \
    && apt-get install -y --no-install-recommends \
    openjdk-17-jdk wget unzip curl git ca-certificates \
    libxcb-cursor0 xvfb tmux libpulse0 \
    && rm -rf /var/lib/apt/lists/*

ENV JAVA_HOME=/usr/lib/jvm/java-17-openjdk-amd64
ENV ANDROID_SDK_ROOT=/opt/android-sdk
ENV PATH=${PATH}:${ANDROID_SDK_ROOT}/cmdline-tools/latest/bin:${ANDROID_SDK_ROOT}/platform-tools

RUN mkdir -p ${ANDROID_SDK_ROOT}

# Download Android command line tools and install required packages
RUN cd /tmp \
    && wget -q -O commandlinetools.zip https://dl.google.com/android/repository/commandlinetools-linux-9477386_latest.zip \
    && unzip -q commandlinetools.zip -d /tmp/cmdline \
    && mkdir -p ${ANDROID_SDK_ROOT}/cmdline-tools/latest \
    && mv /tmp/cmdline/cmdline-tools/* ${ANDROID_SDK_ROOT}/cmdline-tools/latest/ \
    && rm -rf /tmp/cmdline commandlinetools.zip \
    && yes | ${ANDROID_SDK_ROOT}/cmdline-tools/latest/bin/sdkmanager --sdk_root=${ANDROID_SDK_ROOT} --licenses || true \
    && ${ANDROID_SDK_ROOT}/cmdline-tools/latest/bin/sdkmanager --sdk_root=${ANDROID_SDK_ROOT} --install \
    "platform-tools" \
    "platforms;android-34" \
    "build-tools;34.0.0" \
    "cmdline-tools;11.0" \
    "system-images;android-34;google_apis;x86_64" || true \
    && ${ANDROID_SDK_ROOT}/cmdline-tools/latest/bin/sdkmanager --sdk_root=${ANDROID_SDK_ROOT} --channel=3 --install \
    "emulator" || true \
    && yes | ${ANDROID_SDK_ROOT}/cmdline-tools/latest/bin/sdkmanager --sdk_root=${ANDROID_SDK_ROOT} --licenses || true

RUN apt-get clean && rm -rf /var/lib/apt/lists/*

# Install .NET MAUI Android workload so dotnet restore works out of the box
RUN dotnet workload install maui-android --skip-sign-check

# Create a default AVD (Pixel 8, Android 34) so the emulator is ready to use
ENV ANDROID_AVD_HOME=/root/.android/avd
RUN mkdir -p /root/.android/avd \
    && echo "no" | /opt/android-sdk/cmdline-tools/latest/bin/avdmanager create avd \
    -n Pixel8 \
    -k "system-images;android-34;google_apis;x86_64" \
    -d pixel_6 --force \
    && echo "no" | /opt/android-sdk/cmdline-tools/latest/bin/avdmanager create avd \
    -n Pixel8b \
    -k "system-images;android-34;google_apis;x86_64" \
    -d pixel_6 --force

WORKDIR /workspace
