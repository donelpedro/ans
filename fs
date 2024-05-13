---
# Tasks to manage filesystem mounts, sizes, fstypes, and swap
- name: Ensure the filesystems are correctly managed
  block:
    - name: Gather mounted filesystems and swaps
      ansible.builtin.setup:
        gather_subset:
          - "mounts"

    - name: Define list of managed paths and sources from dictionary
      set_fact:
        managed_paths: "{{ nvs_linux_core_filesystem_mounts | dict2items | map(attribute='value.path') | list }}"
        managed_sources: "{{ nvs_linux_core_filesystem_mounts | dict2items | map(attribute='value.src') | list }}"

    - name: Remove unlisted filesystems and swaps
      ansible.builtin.mount:
        path: "{{ item.mount }}"
        src: "{{ item.device }}"
        fstype: "{{ item.fstype }}"
        state: absent
      loop: "{{ ansible_mounts }}"
      when: item.mount not in managed_paths and item.device not in managed_sources and item.fstype != 'swap'
      register: unmounted

    - name: Disable unlisted swaps
      ansible.builtin.swapfile:
        path: "{{ item.device }}"
        state: absent
      loop: "{{ ansible_mounts }}"
      when: item.mount == 'none' and item.device not in managed_sources and item.fstype == 'swap'

    - name: Create or resize logical volume for LVM paths or partitions
      community.general.lvol:
        vg: "{{ item.value.src | regex_replace('/dev/(.*)/.*', '\\1') }}"
        lv: "{{ item.value.src | regex_replace('/dev/.*/(.*)', '\\1') }}"
        size: "{{ item.value.size }}"
        resizefs: yes
        state: present
      loop: "{{ nvs_linux_core_filesystem_mounts | dict2items }}"
      when: item.value.src is match("/dev/.*/.*") and item.value.swap != true

    - name: Create swap area
      ansible.builtin.swapfile:
        path: "{{ item.value.src }}"
        size: "{{ item.value.size }}"
        state: present
      loop: "{{ nvs_linux_core_filesystem_mounts | dict2items }}"
      when: item.value.swap == true

    - name: Create mount point for filesystems
      ansible.builtin.file:
        path: "{{ item.value.path }}"
        state: directory
        owner: root
        group: root
        mode: '0755'
      loop: "{{ nvs_linux_core_filesystem_mounts | dict2items }}"
      when: item.value.swap != true

    - name: Mount the filesystem
      ansible.builtin.mount:
        path: "{{ item.value.path }}"
        src: "{{ item.value.src }}"
        fstype: "{{ item.value.fstype }}"
        opts: defaults
        state: mounted
      loop: "{{ nvs_linux_core_filesystem_mounts | dict2items }}"
      when: item.value.swap != true
      notify: Mount changes
      register: filesystem_mounted

  when: nvs_linux_core_filesystem_mounts is defined
